using System.Threading.Tasks;

public static class SystemExtensions {
    public static bool IsFinished(this Task task) {
        return task.IsCompleted || task.IsFaulted || task.IsCanceled;
    }
}
