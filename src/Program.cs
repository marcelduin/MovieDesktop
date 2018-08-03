using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Vlc.DotNet.Forms;


// MovieDesktop.exe, (c) 2018, marcel@q42.nl
// MIT License

// App Icon from http://www.iconarchive.com/show/iwindows-icons-by-wwalczyszyn/Videos-Library-icon.html

namespace MovieDesktop
{
  class Player : VlcControl
  {
    [STAThread]
    static void Main(string[] args)
    {
      string source = "";
      int screenNum = Properties.Settings.Default.ScreenIdx + 1;

      if (args.Length >= 1 && !String.IsNullOrWhiteSpace(args[0]))
      {
        source = args[0];
      }
      else if(!String.IsNullOrWhiteSpace(Properties.Settings.Default.VideoSrc))
      { // Read previously selected image from settings file
        source = Properties.Settings.Default.VideoSrc;
      }
      else // Display an OpenFileDialog
      {
        source = SelectFile();
      }

      if (String.IsNullOrWhiteSpace(source))
      {
        MessageBox.Show("No input file or url given!", "Error");
        throw new ArgumentException("No input file or url given");
      }

      // Read the screen number from input
      if (args.Length == 2) Int32.TryParse(args[1], out screenNum);

      Application.Run(new Player(source, screenNum-1).FindForm());

    }

    public Player(string videoSrc, int screenIdx = 0)
    {
      Text = "Fullscreen desktop movie";

      // Get the desktop process
      var desktop = GetWorkerW();
      if(desktop == IntPtr.Zero)
      {
        MessageBox.Show("Desktop process not found.", "Error");
        throw new Exception("Desktop process not found.");
      }

      // Get VLC libraries
      bool isX86 = IntPtr.Size == 4;
      Console.WriteLine("Running in " + (isX86 ? "32-bit" : "64-bit"));

      var vlcPath = new DirectoryInfo(isX86 ? "C:\\Program Files (x86)\\VideoLAN\\VLC" : "C:\\Program Files\\VideoLAN\\VLC");
      if (!vlcPath.Exists)
      {
        MessageBox.Show((isX86 ? "32-bit" : "64-bit") + " version of VLC not found on your system. Please install.", "Error");
        throw new EntryPointNotFoundException("VLC not found on your system.");
      }

      VlcLibDirectory = vlcPath;
      EndInit();

      // Place tray icon
      CreateNotifyicon();

      // No audio
      Audio.IsMute = true;

      // Error handling
      EncounteredError += (sender, e) =>
      {
        MessageBox.Show("An error occurred - " + e.ToString(), "Error");
        throw new Exception(e.ToString());
      };

      // When the program is exited, reset the desktop to original backgrounds
      Application.ThreadExit += new EventHandler((s, e) => Exit());

      // Set the desktop process as parent process
      SetParent(Handle, desktop);

      // Set screen
      SetDesktop(screenIdx);

      // Open and play video
      Open(videoSrc);

    }

    private void Exit()
    {
      Console.WriteLine("Gracefully exiting..");

      // Stop any playing video
      Stop();

      // Dispose of video
      Dispose();

      // Dispose tray icon
      notifyIcon.Dispose();

      // Reset the original desktop wallpaper
      ResetDesktop();

    }

    private void Open(string videoSrc)
    {
      Stop();

      Properties.Settings.Default.VideoSrc = videoSrc;

      // If non-url given, check if local file or dir exists
      if (!videoSrc.StartsWith("http") && !videoSrc.StartsWith("file://"))
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
            MessageBox.Show("No valid video files (mp4, webm, avi) found in directory!");
            throw new FileNotFoundException();
          }

          file = new FileInfo(files[rand.Next(files.Length)]);

          Console.WriteLine("Provided a directory. Random pick: " + file.Name);

        }
        else if (!file.Exists)
        {
          MessageBox.Show("File doesn't exist");
          throw new FileNotFoundException("Video file doesn't exist!");
        }

        // Convert to file:// format
        videoSrc = new Uri(file.FullName).AbsoluteUri;
      }

      // If OK, save settings
      Properties.Settings.Default.Save();

      // Set media and loop infinitely (65535 instead of -1 due to a bug in some VLCs)
      SetMedia(videoSrc, new string[] { "input-repeat=65535" });

      // Play
      Play();

    }

    private static string SelectFile()
    {
      OpenFileDialog dialog = new OpenFileDialog
      {
        Filter = "Video files|*.mp4;*.webm;*.avi",
        Title = "Select Video File"
      };

      if (dialog.ShowDialog() == DialogResult.OK)
      {
        return dialog.FileName;
      }

      return "";

    }

    private void SetDesktop(int screenIdx)
    {
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

      // Place the video on the correct screen
      Top = top;
      Left = left;

      // Set settings
      Properties.Settings.Default.ScreenIdx = screenIdx;
      Properties.Settings.Default.Save();

      // Redraw original desktop bg on prev desktop
      ResetDesktop();

    }

    private void ResetDesktop()
    {
      // Reset the desktop background image to prevent frozen video or black screen on exit
      StringBuilder sb = new StringBuilder(300);
      SystemParametersInfo(SPI_GETDESKWALLPAPER, 300, sb, 0);
      SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, sb, 0x2);
    }


    /////////////////////////////////////////////////////////
    // Tray icon
    // From https://social.msdn.microsoft.com/Forums/vstudio/en-US/0913ae1a-7efc-4d7f-a7f7-58f112c69f66/c-application-system-tray-icon?forum=csharpgeneral
    private NotifyIcon notifyIcon;
    private ContextMenu contextMenu;
    private MenuItem menuOpen;
    private MenuItem menuScreen;
    private MenuItem menuExit;
    private System.ComponentModel.IContainer components;

    private void CreateNotifyicon()
    {
      components = new System.ComponentModel.Container();
      contextMenu = new ContextMenu();

      menuOpen = new MenuItem
      {
        Index = 0,
        Text = "&Open video..."
      };

      menuOpen.Click += new EventHandler((s, e) =>
      {
        string newFile = SelectFile();
        if (!String.IsNullOrWhiteSpace(newFile))
          Open(newFile);
      });

      contextMenu.MenuItems.Add(menuOpen);

      // If multiple screens, add screen selector
      var screens = Screen.AllScreens.Count();

      if(screens > 1)
      {
        menuScreen = new MenuItem
        {
          Index = 1,
          Text = "Select screen"
        };

        for(int i = 0; i < screens; i++)
        {
          var screen = Screen.AllScreens[i];
          var setScreen = new MenuItem
          {
            Index = i,
            Text = (i+1).ToString() + " - " + screen.Bounds.Width + " x " + screen.Bounds.Height + (screen.Primary ? " (primary)" : "")
          };

          setScreen.Click += new EventHandler((s, e) => {
            SetDesktop(((MenuItem)s).Index);
          });

          menuScreen.MenuItems.Add(setScreen);

        }

        contextMenu.MenuItems.Add(menuScreen);
      }


      // Add exit button
      menuExit = new MenuItem
      {
        Index = 2,
        Text = "E&xit"
      };

      menuExit.Click += new EventHandler((s,e) => {
        // Close the form, which closes the application.
        Application.Exit();
      });

      contextMenu.MenuItems.Add("-");

      contextMenu.MenuItems.Add(menuExit);

      // Create the NotifyIcon.
      notifyIcon = new NotifyIcon(components)
      {

        // The Icon property sets the icon that will appear
        // in the systray for this application.
        Icon = Properties.Resources.app,

        // The ContextMenu property sets the menu that will
        // appear when the systray icon is right clicked.
        ContextMenu = contextMenu,

        // The Text property sets the text that will be displayed,
        // in a tooltip, when the mouse hovers over the systray icon.
        Text = "MovieDesktop",
        Visible = true
      };

    }


    /////////////////////////////////////////////////////////
    // Call for resetting desktop background after exit
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern int SystemParametersInfo(int uAction, int uParam, StringBuilder lpvParam, int fuWinIni);
    public const int SPI_GETDESKWALLPAPER = 0x0073;
    public const int SPI_SETDESKWALLPAPER = 0x0014;


    /////////////////////////////////////////////////////////
    /// Detect ctrl+c/break
    /// From http://geekswithblogs.net/mrnat/archive/2004/09/23/11594.aspx
    [DllImport("Kernel32.dll")]
    public static extern bool SetConsoleCtrlHandler(HandlerRoutine Handler, bool Add);
    // A delegate type to be used as the handler routine for SetConsoleCtrlHandler.
    public delegate bool HandlerRoutine(CtrlTypes CtrlType);
    // An enumerated type for the control messages sent to the handler routine.
    public enum CtrlTypes
    {
      CTRL_C_EVENT = 0,
      CTRL_BREAK_EVENT,
      CTRL_CLOSE_EVENT,
      CTRL_LOGOFF_EVENT = 5,
      CTRL_SHUTDOWN_EVENT
    }

    /////////////////////////////////////////////////////////
    /// Get processId of actual desktop background renderer
    /// From https://www.codeproject.com/Articles/856020/Draw-behind-Desktop-Icons-in-Windows
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
      SendMessageTimeout(progman, 0x052C, new IntPtr(0), IntPtr.Zero, SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out result);

      IntPtr workerw = IntPtr.Zero;

      // We enumerate all Windows, until we find one, that has the SHELLDLL_DefView
      // as a child.
      // If we found that window, we take its next sibling and assign it to workerw.
      EnumWindows(new EnumWindowsProc((tophandle, topparamhandle) =>
      {
        IntPtr p = FindWindowEx(tophandle, IntPtr.Zero, "SHELLDLL_DefView", "");

        if (p != IntPtr.Zero)
        {
          // Gets the WorkerW Window after the current one.
          workerw = FindWindowEx(IntPtr.Zero, tophandle, "WorkerW", "");
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
