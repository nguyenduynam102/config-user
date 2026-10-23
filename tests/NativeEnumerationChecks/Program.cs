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
var setFlags=Marshal.GetDelegateForFunctionPointer<SetFlags>(NativeLibrary.GetExport(library,"TestFlags"));
var lastFlags=Marshal.GetDelegateForFunctionPointer<GetFlags>(NativeLibrary.GetExport(library,"TestLastFlags"));
var setCode=Marshal.GetDelegateForFunctionPointer<SetMode>(NativeLibrary.GetExport(library,"TestSetCode"));
var passwordOk=Marshal.GetDelegateForFunctionPointer<Count>(NativeLibrary.GetExport(library,"TestPasswordOk"));
Check("unlock clears lockout while preserving disabled and unrelated flags",()=>{setFlags(0x10212);AccountUpdates.Flags("alpha",f=>f&~0x10u);if(lastFlags()!=0x10202)throw new Exception("Flags corrupted");});
Check("enable preserves lockout and unrelated flags",()=>{setFlags(0x10212);AccountUpdates.Flags("alpha",f=>f&~2u);if(lastFlags()!=0x10210)throw new Exception("Flags corrupted");});
Check("password reset uses USER_INFO_1003 UTF16 pointer",()=>{AccountUpdates.Password("alpha","fixture-only");if(passwordOk()!=1)throw new Exception("Wrong password marshaling");});
Check("flag update failure frees level-1 buffer",()=>{setCode(5);try{AccountUpdates.Flags("alpha",f=>f);throw new Exception("Expected failure");}catch(Win32Exception e){if(e.NativeErrorCode!=5)throw;}finally{setCode(0);}});
Check("flag transformation failure frees native buffer",()=>{try{AccountUpdates.Flags("alpha",_=>throw new ArgumentException("fixture"));throw new Exception("Expected failure");}catch(ArgumentException){}});
Console.WriteLine($"{passed} native-contract checks passed (Linux fixture; Windows runtime NOT tested).");
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void SetMode(int mode);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate int Count();

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate void SetFlags(uint flags);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]delegate uint GetFlags();
