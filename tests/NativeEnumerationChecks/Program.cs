using System.ComponentModel;
using System.Runtime.InteropServices;
using WinUser;
if(args.Length!=1)throw new ArgumentException("Pass the absolute path of libnetapi_fixture.so");
var library=NativeLibrary.Load(args[0]);
NativeLibrary.SetDllImportResolver(typeof(AccountEnumeration).Assembly,(name,_,_)=>name=="Netapi32.dll"?library:IntPtr.Zero);
var mode=Marshal.GetDelegateForFunctionPointer<SetMode>(NativeLibrary.GetExport(library,"TestMode"));
var outstanding=Marshal.GetDelegateForFunctionPointer<Count>(NativeLibrary.GetExport(library,"TestOutstandingBuffers"));
int passed=0;
void Check(string label,Action action){action();if(outstanding()!=0)throw new Exception("Native buffer leak");Console.WriteLine("PASS "+label);passed++;}
Check("enumerates level 0, fetches level 23, preserves flags and SID across pages, skips deleted account",()=>{
 var users=AccountEnumeration.Read();
 if(users.Count!=2||users[0].Name!="alpha"||users[1].Name!="beta"||users[0].Flags!=0x200||users[1].Flags!=0x202||BitConverter.ToUInt32(users[1].Sid,12)!=1002)throw new Exception("Wrong native mapping");
});
foreach(var scenario in new[]{1,2})Check("native failure "+scenario+" propagates and frees buffers",()=>{
 mode(scenario);
 try{AccountEnumeration.Read();throw new Exception("Expected Win32Exception");}catch(Win32Exception e){if(e.NativeErrorCode!=5||!e.Message.Contains("NetUser"))throw;}
});
Check("empty enumeration",()=>{mode(3);if(AccountEnumeration.Read().Count!=0)throw new Exception("Expected empty result");});
Check("repeat enumeration releases native allocations",()=>{mode(0);for(int i=0;i<100;i++)if(AccountEnumeration.Read().Count!=2)throw new Exception("Wrong count");});
Console.WriteLine($"{passed} native-contract checks passed (Linux fixture; Windows runtime NOT tested).");
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void SetMode(int mode);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Count();
