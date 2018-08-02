using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Vlc.DotNet.Forms;

namespace MovieDesktop
{
  class Player : Form
  {
    private VlcControl video;

    [STAThread]
    static void Main(string[] args)
    {
#if DEBUG
      // Sample video on first screen
      Application.Run(new Player("https://i.imgur.com/VMb5aPE.mp4", 0));
#else
      if (args.Length < 1) throw new ArgumentException("No input file or url given");
      uint screenNum = 1;
      if (args.Length == 2) UInt32.TryParse(args[1], out screenNum);

      Application.Run(new Player(args[0], screenNum-1));
#endif
    }

    public Player(string videoSrc, uint screenIdx = 0)
    {
      var desktop = GetWorkerW();

      if(desktop == IntPtr.Zero)
      {
        Console.Error.WriteLine("Desktop process not found.");
        throw new Exception("Desktop process not found.");
      }

      bool isX86 = IntPtr.Size == 4;
      Console.WriteLine("Running in " + (isX86 ? "32-bit" : "64-bit"));

      var vlcPath = new DirectoryInfo(isX86 ? "C:\\Program Files (x86)\\VideoLAN\\VLC" : "C:\\Program Files\\VideoLAN\\VLC");
      if (!vlcPath.Exists)
      {
        Console.Error.WriteLine("Error: " + (isX86 ? "32-bit" : "64-bit") + " version of VLC not found on your system. Please install.");
        throw new EntryPointNotFoundException("VLC not found on your system.");
      }


      // If non-url given, check if local file exists
      if (!videoSrc.StartsWith("http"))
      {
        FileInfo file = new FileInfo(videoSrc);
        if (!file.Exists)
        {
          Console.Error.WriteLine("Doesn't exist dude");
          throw new FileNotFoundException("Video file doesn't exist!");
        }

        // Convert to file:// format
        videoSrc = new Uri(file.FullName).AbsoluteUri;
      }


      /// Forms part
      Text = "Fullscreen desktop movie";
      FormBorderStyle = FormBorderStyle.None;

      /// Video part
      video = new VlcControl
      {
        VlcLibDirectory = vlcPath
      };
      video.EndInit();

      // Loop infinitely
      video.SetMedia(videoSrc, new string[]{"input-repeat=65535"});

      // No audio
      video.Audio.IsMute = true;

      // Error handling
      video.EncounteredError += (sender, e) =>
      {
        Console.Error.Write("An error occurred - " + e);
        throw new Exception(e.ToString());
      };

      // Add to main form
      Controls.Add(video);


      /// Screen part
      // Try to get preferred screen, otherwise just get first
      var screen = screenIdx < Screen.AllScreens.Count() ? Screen.AllScreens[screenIdx] : Screen.AllScreens.First();

      video.Width = Width = screen.WorkingArea.Width;
      video.Height = Height = screen.WorkingArea.Height;

      // Desktop Window has [0,0] as top left screen.. get top and left for selected screen
      int top = 0;
      int left = 0;
      foreach (Screen s in Screen.AllScreens.Where(s => s != screen))
      {
        top += Math.Max(0, -s.WorkingArea.Top);
        left += Math.Max(0, -s.WorkingArea.Left);
      }

      Load += new EventHandler((s, e) =>
      {
        Top = top;
        Left = left;
        SetParent(Handle, desktop);

        // Play video
        video.Play();
      });

    }

    // Imports
    [Flags]
    enum SendMessageTimeoutFlags : uint
    {
      SMTO_NORMAL = 0x0,
      SMTO_BLOCK = 0x1,
      SMTO_ABORTIFHUNG = 0x2,
      SMTO_NOTIMEOUTIFNOTHUNG = 0x8,
      SMTO_ERRORONEXIT = 0x20
    }
    [DllImport("User32", SetLastError = true)]
    static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    static extern IntPtr SendMessageTimeout(
    IntPtr windowHandle,
    uint Msg,
    IntPtr wParam,
    IntPtr lParam,
    SendMessageTimeoutFlags flags,
    uint timeout,
    out IntPtr result);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("User32", SetLastError = true)]
    static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
    [DllImport("user32.dll", SetLastError = true)]
    static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

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
