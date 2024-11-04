using System.Diagnostics;
using System.Text;

public class UTF8TraceListener : TextWriterTraceListener
{
    public UTF8TraceListener(string fileName) : base(fileName)
    {
        var stream = new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read);
        Writer = new StreamWriter(stream, Encoding.UTF8);
    }

    public override void WriteLine(string message)
    {
        base.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        Flush(); // 确保立即写入文件
    }

    public override void Write(string message)
    {
        base.Write($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}");
        Flush(); // 确保立即写入文件
    }
}
