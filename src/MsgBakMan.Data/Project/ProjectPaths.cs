namespace MsgBakMan.Data.Project;

public sealed class ProjectPaths
{
    public ProjectPaths(string projectRoot)
    {
        ProjectRoot = projectRoot;
        DbPath = Path.Combine(projectRoot, "db", "messages.sqlite");
        MediaRoot = Path.Combine(projectRoot, "media");
        MediaBlobRoot = Path.Combine(MediaRoot, "blobs");
        TempRoot = Path.Combine(MediaRoot, "temp");
    }

    public string ProjectRoot { get; }
    public string DbPath { get; }
    public string MediaRoot { get; }
    public string MediaBlobRoot { get; }
    public string TempRoot { get; }
}
