using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

sealed class KeyboardListener : NativeWindow {
    const int WmInput = 0xff;
    const int WmQuit = 0x12;
    const uint InputSink = 0x100;
    const uint ReadInput = 0x10000003;

    [StructLayout(LayoutKind.Sequential)]
    struct RawDevice {
        public ushort Page, Usage;
        public uint Flags;
        public IntPtr Window;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct RawHeader {
        public uint Type, Size;
        public IntPtr Device, Param;
    }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    struct RawKeyboard {
        public RawHeader Header;
        public ushort Scan, Flags, Reserved, Key;
        public uint Message, Extra;
    }

    [DllImport("user32.dll", SetLastError = true)]
    static extern bool RegisterRawInputDevices(RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll")]
    static extern uint GetRawInputData(IntPtr input, uint command, out RawKeyboard data,
                                       ref uint size, uint headerSize);
    [DllImport("user32.dll")]
    static extern bool PostThreadMessage(uint thread, uint message, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")]
    static extern uint GetCurrentThreadId();

    sealed class Bindings {
        public readonly int Generation;
        public readonly int[] Keys;
        public Bindings(int generation, int[] keys) {
            Generation = generation;
            Keys = keys;
        }
    }

    volatile Bindings bindings = new Bindings(0, new int[0]);
    readonly bool[] pressed = new bool[256];
    static readonly uint keyboardSize = (uint)Marshal.SizeOf(typeof(RawKeyboard));
    static readonly uint headerSize = (uint)Marshal.SizeOf(typeof(RawHeader));

    KeyboardListener() {
        CreateHandle(new CreateParams { Parent = new IntPtr(-3) });
        var device = new RawDevice { Page = 1, Usage = 6, Flags = InputSink, Window = Handle };
        if (!RegisterRawInputDevices(new[] { device }, 1, (uint)Marshal.SizeOf(typeof(RawDevice))))
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    void ReadBindings(uint windowThread) {
        try {
            string line;
            while ((line = Console.ReadLine()) != null) {
                var parts = line.Split(' ');
                int generation;
                if (!int.TryParse(parts[0], out generation)) continue;
                var keys = new int[parts.Length - 1];
                for (int i = 0; i < keys.Length; i++) int.TryParse(parts[i + 1], out keys[i]);
                bindings = new Bindings(generation, keys);
            }
        } finally {
            PostThreadMessage(windowThread, WmQuit, IntPtr.Zero, IntPtr.Zero);
        }
    }

    static int VirtualKey(RawKeyboard input) {
        bool extended = (input.Flags & 2) != 0;
        switch (input.Key) {
            case 0x10: return input.Scan == 0x36 ? 0xa1 : 0xa0;
            case 0x11: return extended ? 0xa3 : 0xa2;
            case 0x12: return extended ? 0xa5 : 0xa4;
        }
        // Windows reports navigation keys for the number pad when Num Lock is off.
        if (!extended && input.Key >= 0x21 && input.Key <= 0x2e) {
            switch (input.Scan) {
                case 0x52: return 0x60;
                case 0x4f: return 0x61;
                case 0x50: return 0x62;
                case 0x51: return 0x63;
                case 0x4b: return 0x64;
                case 0x4d: return 0x66;
                case 0x47: return 0x67;
                case 0x48: return 0x68;
                case 0x49: return 0x69;
                case 0x53: return 0x6e;
            }
        }
        if (!extended && input.Key == 0x0c && input.Scan == 0x4c) return 0x65;
        return input.Key;
    }

    void ReadKey(IntPtr handle) {
        RawKeyboard input;
        uint size = keyboardSize;
        uint bytes = GetRawInputData(handle, ReadInput, out input, ref size, headerSize);
        if (bytes < headerSize + 16 || bytes > keyboardSize || input.Header.Type != 1)
            return;
        int key = VirtualKey(input);
        if (key < 1 || key >= 255) return;
        bool down = (input.Flags & 1) == 0;
        bool wasDown = pressed[key];
        pressed[key] = down;
        if (!down || wasDown || pressed[0x5b] || pressed[0x5c]) return;
        int modifiers = (pressed[0xa2] || pressed[0xa3] ? 1 : 0) |
                        (pressed[0xa4] || pressed[0xa5] ? 2 : 0) |
                        (pressed[0xa0] || pressed[0xa1] ? 4 : 0);
        Bindings current = bindings;
        int index = Array.IndexOf(current.Keys, key | (modifiers << 8));
        if (index >= 0) Console.WriteLine("{0} {1}", current.Generation, index);
    }

    protected override void WndProc(ref Message message) {
        if (message.Msg == WmInput) ReadKey(message.LParam);
        base.WndProc(ref message);
    }

    [STAThread]
    static void Main() {
        var listener = new KeyboardListener();
        uint thread = GetCurrentThreadId();
        var reader = new Thread(() => listener.ReadBindings(thread)) { IsBackground = true };
        reader.Start();
        Console.WriteLine("ready");
        Application.Run();
        listener.DestroyHandle();
    }
}
