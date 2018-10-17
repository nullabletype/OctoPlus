namespace OctoPlus.Console.ConsoleTools {
    public interface IProgressBar {
        void CleanCurrentLine();
        void WriteProgress(int current, int total, string message);
        void WriteStatusLine(string status);
    }
}