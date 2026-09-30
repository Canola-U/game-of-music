using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// mp4 → webm(VP8) 변환 한 건을 ffmpeg 프로세스로 돌린다. 에디터 자동 변환과 게임 안 자동 변환이 같이 쓴다.
// mp4(H.264)는 Windows 디코더를 거쳐서 PC에 따라 첫 프레임에서 멈추지만,
// VP8은 Unity가 자체 디코더로 풀어서 어디서나 재생된다.
public class WebmConversion
{
    public const int targetFps = 60;

    public readonly string source;   // 원본 mp4
    public readonly string output;   // 같은 이름의 webm

    public double DurationSec { get; private set; }
    public double DoneSec { get; private set; }
    public bool IsFinished { get; private set; }
    public bool Succeeded { get; private set; }

    public float Progress => DurationSec > 0 ? (float)Math.Min(1.0, DoneSec / DurationSec) : 0f;

    public string Log
    {
        get { lock (log) return log.ToString(); }
    }

    static readonly Regex durationRegex = new Regex(@"Duration: (\d+):(\d+):(\d+(?:\.\d+)?)");

    readonly string temp;
    readonly StringBuilder log = new StringBuilder();
    Process process;
    volatile bool exited;
    int exitCode;

    WebmConversion(string source)
    {
        this.source = source;
        output = Path.ChangeExtension(source, ".webm");
        // 변환 중인 파일이 곡 폴더에 보이지 않도록 임시 폴더에 만들고, 끝나면 옮긴다
        temp = Path.Combine(Path.GetTempPath(), "gom_" + Guid.NewGuid().ToString("N") + ".webm");
    }

    // webm이 없거나 mp4보다 오래됐으면 변환이 필요하다
    public static bool NeedsConversion(string mp4Path)
    {
        string webm = Path.ChangeExtension(mp4Path, ".webm");
        return !File.Exists(webm) || File.GetLastWriteTimeUtc(webm) < File.GetLastWriteTimeUtc(mp4Path);
    }

    // lowPriority: 게임 도중에 돌릴 때 게임이 버벅이지 않도록 CPU를 덜 쓴다
    public static WebmConversion Start(string ffmpegPath, string mp4Path, bool lowPriority)
    {
        WebmConversion job = new WebmConversion(mp4Path);

        int cores = Environment.ProcessorCount;
        int threads = lowPriority ? Math.Max(1, cores / 2) : Math.Max(2, cores - 2);
        threads = Math.Min(threads, 16);

        string args =
            $"-hide_banner -nostats -y -i \"{job.source}\" -an " +
            // 해상도는 원본 그대로 (VP8은 짝수 크기가 필요해서 홀수일 때만 1px 맞춤)
            $"-vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2,fps={targetFps},format=yuv420p\" " +
            $"-c:v libvpx -b:v 12M -crf 10 -qmin 4 -qmax 42 -deadline good -cpu-used 4 -threads {threads} " +
            // 키프레임 1초마다 (싱크 맞출 때 빠르게 이동), alt-ref 끔 (Unity 호환)
            $"-g {targetFps} -auto-alt-ref 0 -progress pipe:1 \"{job.temp}\"";

        ProcessStartInfo info = new ProcessStartInfo(ffmpegPath, args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        Process p = new Process { StartInfo = info, EnableRaisingEvents = true };
        p.OutputDataReceived += (s, e) =>
        {
            // -progress 출력: out_time_us=12345678
            if (e.Data != null && e.Data.StartsWith("out_time_us=") &&
                long.TryParse(e.Data.Substring(12), out long us))
                job.DoneSec = us / 1_000_000.0;
        };
        p.ErrorDataReceived += (s, e) =>
        {
            if (e.Data == null) return;
            if (job.DurationSec <= 0)
            {
                Match m = durationRegex.Match(e.Data);
                if (m.Success)
                {
                    job.DurationSec = int.Parse(m.Groups[1].Value) * 3600 + int.Parse(m.Groups[2].Value) * 60 +
                                      double.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
                }
            }
            lock (job.log) job.log.AppendLine(e.Data);
        };
        p.Exited += (s, e) =>
        {
            job.exitCode = p.ExitCode;
            job.exited = true;
        };

        p.Start();
        p.BeginOutputReadLine();
        p.BeginErrorReadLine();
        if (lowPriority)
        {
            try { p.PriorityClass = ProcessPriorityClass.BelowNormal; } catch { }
        }

        job.process = p;
        return job;
    }

    // 매 프레임 불러준다. ffmpeg가 끝났으면 결과 파일을 제자리로 옮기고 true
    public bool Poll()
    {
        if (IsFinished) return true;
        if (!exited) return false;

        bool ok = exitCode == 0 && File.Exists(temp) && new FileInfo(temp).Length > 0;
        if (ok)
        {
            try
            {
                File.Copy(temp, output, true);
            }
            catch (Exception ex)
            {
                ok = false;
                lock (log) log.AppendLine("webm 저장 실패: " + ex.Message);
            }
        }
        if (!ok) lock (log) log.AppendLine($"ffmpeg 종료 코드: {exitCode}");

        TryDelete(temp);
        Succeeded = ok;
        IsFinished = true;
        return true;
    }

    public void Cancel()
    {
        try
        {
            if (process != null && !process.HasExited) process.Kill();
        }
        catch { }
        TryDelete(temp);
        IsFinished = true;
    }

    // 오류 로그가 너무 길면 뒤쪽만
    public string LogTail(int maxChars = 3000)
    {
        string text = Log;
        return text.Length > maxChars ? "…" + text.Substring(text.Length - maxChars) : text;
    }

    static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch { }
    }
}
