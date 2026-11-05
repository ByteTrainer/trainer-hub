using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
class WindowProbe {
 [StructLayout(LayoutKind.Sequential)] struct Rect { public int Left,Top,Right,Bottom; }
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out Rect r);
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int value);
 [DllImport("user32.dll")] static extern bool AttachThreadInput(uint from,uint to,bool attach);
 [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] static extern bool IsGUIThread(bool convert);
 [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
 static bool Focus(IntPtr h){IsGUIThread(true);uint ignored;uint foreground=GetWindowThreadProcessId(GetForegroundWindow(),out ignored),target=GetWindowThreadProcessId(h,out ignored),current=GetCurrentThreadId();bool attached=foreground!=0&&foreground!=current&&AttachThreadInput(current,foreground,true),targetAttached=target!=current&&target!=foreground&&AttachThreadInput(current,target,true);try{BringWindowToTop(h);return SetForegroundWindow(h);}finally{if(targetAttached)AttachThreadInput(current,target,false);if(attached)AttachThreadInput(current,foreground,false);}}
 delegate bool EnumProc(IntPtr h,IntPtr arg);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback,IntPtr arg);
 static string Exe(IntPtr h){uint pid;GetWindowThreadProcessId(h,out pid);try{using(var p=Process.GetProcessById((int)pid))return p.MainModule.FileName;}catch{return null;}}
 static object Info(IntPtr h){Rect r;uint pid;GetWindowThreadProcessId(h,out pid);GetWindowRect(h,out r);return new {handle=h.ToInt64().ToString(),pid=pid,exe=Exe(h),visible=IsWindowVisible(h),minimized=IsIconic(h),bounds=new{x=r.Left,y=r.Top,width=r.Right-r.Left,height=r.Bottom-r.Top}};}
 static void Main(){var json=new JavaScriptSerializer();string line;while((line=Console.ReadLine())!=null){object id=null;try{var q=json.Deserialize<Dictionary<string,object>>(line);id=q["id"];string op=(string)q["op"];object result=null;if(op=="foreground")result=Info(GetForegroundWindow());else if(op=="find"){string expected=(string)q["exe"];IntPtr found=IntPtr.Zero;EnumWindows((h,a)=>{if(IsWindowVisible(h)&&!IsIconic(h)&&String.Equals(Exe(h),expected,StringComparison.OrdinalIgnoreCase)){found=h;return false;}return true;},IntPtr.Zero);if(found!=IntPtr.Zero)result=Info(found);}else if(op=="focus"){var h=new IntPtr(Int64.Parse((string)q["handle"]));if(!String.Equals(Exe(h),(string)q["exe"],StringComparison.OrdinalIgnoreCase))throw new Exception("A janela do jogo mudou.");if(IsIconic(h))ShowWindow(h,9);result=Focus(h);}else throw new Exception("Operação inválida.");Console.WriteLine(json.Serialize(new{id=id,result=result}));}catch(Exception e){Console.WriteLine(json.Serialize(new{id=id,error=e.Message}));}}}
}
