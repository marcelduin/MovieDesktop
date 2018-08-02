using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Vlc.DotNet.Forms;

namespace MovieDesktop
{
  class Player : VlcControl
  {
    [STAThread]
    static void Main(string[] args)
    {
      string source = "";
      uint screenNum = 1;
#if DEBUG
      source = "https://i.imgur.com/VMb5aPE.mp4"; // Sample video
#else
      if (args.Length < 1 || String.IsNullOrWhiteSpace(args[0]))
        throw new ArgumentException("No input file or url given");

      source = args[0];

      if (args.Length == 2) UInt32.TryParse(args[1], out screenNum);
#endif

      Application.Run(new Player(source, screenNum-1).FindForm());
    }

    public Player(string videoSrc, uint screenIdx = 0)
    {
      Text = "Fullscreen desktop movie";

      // Get the desktop process
      var desktop = GetWorkerW();
      if(desktop == IntPtr.Zero)
      {
        Console.Error.WriteLine("Desktop process not found.");
        throw new Exception("Desktop process not found.");
      }

      // Get VLC libraries
      bool isX86 = IntPtr.Size == 4;
      Console.WriteLine("Running in " + (isX86 ? "32-bit" : "64-bit"));

      var vlcPath = new DirectoryInfo(isX86 ? "C:\\Program Files (x86)\\VideoLAN\\VLC" : "C:\\Program Files\\VideoLAN\\VLC");
      if (!vlcPath.Exists)
      {
        Console.Error.WriteLine("Error: " + (isX86 ? "32-bit" : "64-bit") + " version of VLC not found on your system. Please install.");
        throw new EntryPointNotFoundException("VLC not found on your system.");
      }

      VlcLibDirectory = vlcPath;
      EndInit();


      // If non-url given, check if local file or dir exists
      if (!videoSrc.StartsWith("http"))
      {
        FileInfo file = new FileInfo(videoSrc);

        // If it's a directory, get a random video from in there
        if (file.Attributes.HasFlag(FileAttributes.Directory))
        {
          var rand = new Random();
          var allowedExtensions = new HashSet<string>(new string[] { ".mp4", ".webm", ".avi" }, StringComparer.OrdinalIgnoreCase);
          var files = Directory.EnumerateFiles(videoSrc).Where(f => allowedExtensions.Contains(Path.GetExtension(f))).ToArray();

          if (files.Length == 0)
          {
            Console.Error.WriteLine("No valid video files (mp4, webm, avi) found in directory!");
            throw new FileNotFoundException();
          }

          file = new FileInfo(files[rand.Next(files.Length)]);

          Console.WriteLine("Provided a directory. Random pick: " + file.Name);

        }
        else if (!file.Exists)
        {
          Console.Error.WriteLine("File doesn't exist");
          throw new FileNotFoundException("Video file doesn't exist!");
        }

        // Convert to file:// format
        videoSrc = new Uri(file.FullName).AbsoluteUri;
      }

      // Set media and loop infinitely (65535 instead of -1 due to a bug in some VLCs)
      SetMedia(videoSrc, new string[]{"input-repeat=65535"});

      // No audio
      Audio.IsMute = true;

      // Error handling
      EncounteredError += (sender, e) =>
      {
        Console.Error.Write("An error occurred - " + e);
        throw new Exception(e.ToString());
      };

      /// Screen part
      // Try to get preferred screen, otherwise just get first
      var screen = screenIdx < Screen.AllScreens.Count() ? Screen.AllScreens[screenIdx] : Screen.AllScreens.First();

      Width = screen.WorkingArea.Width;
      Height = screen.WorkingArea.Height;

      // Desktop Window has [0,0] as top left screen.. get top and left for selected screen
      int top = 0;
      int left = 0;
      foreach (Screen s in Screen.AllScreens.Where(s => s != screen))
      {
        top += Math.Max(0, -s.WorkingArea.Top);
        left += Math.Max(0, -s.WorkingArea.Left);
      }

      // Set the desktop as parent process
      SetParent(Handle, desktop);

      // Place the video on the correct screen
      Top = top;
      Left = left;

      // Play
      Play();

      // Watch for ctrl+c
      SetConsoleCtrlHandler(new HandlerRoutine(ConsoleCtrlCheck), true);

      // When the program is exited, reset the desktop to original backgrounds
      Application.ThreadExit += new EventHandler((s, e) =>
      {
        Console.WriteLine("gracefully exiting..");

        // Todo fix this. Ending the desktop WorkerW task resets the entire explorer
        //EndTask(desktop, true, true);
      });

    }

    /// <summary>
    /// Kill the WorkerW desktop process to remove the custom overlay when exiting program
    /// From http://geekswithblogs.net/mrnat/archive/2004/09/23/11594.aspx
    /// </summary>
    /// <param name="Handler"></param>
    /// <param name="Add"></param>
    /// <returns></returns>
    [DllImport("Shell32.dll")]
    private static extern int SHChangeNotify(int eventId, int flags, IntPtr item1, IntPtr item2);
    [DllImport("Kernel32.dll")]
    public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
    // A delegate type to be used as the handler routine
    // for SetConsoleCtrlHandler.
    public delegate bool HandlerRoutine(CtrlTypes CtrlType);

    // An enumerated type for the control messages
    // sent to the handler routine.
    public enum CtrlTypes
    {
      CTRL_C_EVENT = 0,
      CTRL_BREAK_EVENT,
      CTRL_CLOSE_EVENT,
      CTRL_LOGOFF_EVENT = 5,
      CTRL_SHUTDOWN_EVENT
    }

    private static bool ConsoleCtrlCheck(CtrlTypes ctrlType)
    {
      Application.Exit();
      return true;
    }

    /// <summary>
    /// All imports required for getting the WorkerW processId
    /// </summary>
    [Flags]
    enum SendMessageTimeoutFlags : uint
    {
      SMTO_NORMAL = 0x0,
      SMTO_BLOCK = 0x1,
      SMTO_ABORTIFHUNG = 0x2,
      SMTO_NOTIMEOUTIFNOTHUNG = 0x8,
      SMTO_ERRORONEXIT = 0x20
    }
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SendMessageTimeout(IntPtr windowHandle, uint Msg, IntPtr wParam, IntPtr lParam, SendMessageTimeoutFlags flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll", SetLastError = true)]
    static extern bool EndTask(IntPtr hWnd, bool fShutDown, bool fForce);
    /// <summary>
    /// Get processId of actual desktop background renderer
    /// From https://www.codeproject.com/Articles/856020/Draw-behind-Desktop-Icons-in-Windows
    /// </summary>
    /// <returns>IntPtr to WorkerW processId</returns>

    private static IntPtr GetWorkerW()
    {

      // Fetch the Progman window
      IntPtr progman = FindWindow("Progman", null);

#if DEBUG
      Console.WriteLine("progman process: " + progman);
#endif

      IntPtr result = IntPtr.Zero;

      // Send 0x052C to Progman. This message directs Progman to spawn a
      // WorkerW behind the desktop icons. If it is already there, nothing
      // happens.
      SendMessageTimeout(progman,
                             0x052C,
                             new IntPtr(0),
                             IntPtr.Zero,
                             SendMessageTimeoutFlags.SMTO_NORMAL,
                             1000,
                             out result);

      IntPtr workerw = IntPtr.Zero;

      // We enumerate all Windows, until we find one, that has the SHELLDLL_DefView
      // as a child.
      // If we found that window, we take its next sibling and assign it to workerw.
      EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
      {
        IntPtr p = FindWindowEx(tophandle,
                                    IntPtr.Zero,
                                    "SHELLDLL_DefView",
                                    "");

        if (p != IntPtr.Zero)
        {
          // Gets the WorkerW Window after the current one.
          workerw = FindWindowEx(IntPtr.Zero,
                                     tophandle,
                                     "WorkerW",
                                     "");
        }

        return true;
      }), IntPtr.Zero);

#if DEBUG
      Console.WriteLine("workerw process: " + workerw);
#endif

      return workerw;
    }


  }
}
