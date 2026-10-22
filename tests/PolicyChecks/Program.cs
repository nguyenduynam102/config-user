using WinUser;
int passed=0;
void Check(string name,Action check){try{check();Console.WriteLine("PASS "+name);passed++;}catch(Exception e){Console.Error.WriteLine("FAIL "+name+": "+e.Message);Environment.ExitCode=1;}}
void Equal<T>(T expected,T actual){if(!Equals(expected,actual))throw new Exception($"Expected {expected}, got {actual}");}
void Reject(Action action){try{action();}catch(ArgumentException){return;}throw new Exception("Invalid input accepted");}
Check("local username",()=>Equal("nhanvien",Policy.UserName(" nhanvien ")));
foreach(var n in new[]{"","a/b","a\\b","a.","a\nb","123456789012345678901"})Check("reject username "+n.Replace('\n','?'),()=>Reject(()=>Policy.UserName(n)));
Check("shortcut lnk",()=>Equal("Ứng dụng.lnk",Policy.Shortcut("Ứng dụng.lnk")));
Check("shortcut url",()=>Equal("nas.URL",Policy.Shortcut("nas.URL")));
foreach(var n in new[]{"../a.lnk","..\\a.lnk","a.txt","a.lnk:secret","/a.url"})Check("reject shortcut "+n,()=>Reject(()=>Policy.Shortcut(n)));
Check("UNC",()=>Equal(@"\\nas\share\team",Policy.Unc(@"\\nas\share\team")));
foreach(var u in new[]{"Z:\\share",@"\\nas",@"\\?\C:\data",@"\\nas\share\..\other",@"\\nas\share:stream"})Check("reject UNC "+u,()=>Reject(()=>Policy.Unc(u)));
Check("drive normalize",()=>Equal("Z:",Policy.Drive("z")));
foreach(var d in new[]{"C:","ZZ:","Z:\\","1:"})Check("reject drive "+d,()=>Reject(()=>Policy.Drive(d)));
const string header="Windows Registry Editor Version 5.00\n";
Check("parse per user REG",()=>{var r=Policy.ParseRegistry(header+"[HKEY_CURRENT_USER\\Software\\WinUserTest]\n\"Text\"=\"Tiếng Việt\"\n\"Number\"=dword:ffffffff\n@=\"default\"");Equal(3,r.Count);Equal("Software\\WinUserTest",r[0].Key);Equal("Tiếng Việt",r[0].Text);Equal(-1,r[1].Number);Equal("",r[2].Name);});
Check("REG escapes",()=>Equal("C:\\Tools\\a.exe",Policy.ParseRegistry(header+"[HKEY_CURRENT_USER\\Software\\Test]\n\"Path\"=\"C:\\\\Tools\\\\a.exe\"")[0].Text));
foreach(var key in new[]{"HKEY_LOCAL_MACHINE\\Software\\Test","-HKEY_CURRENT_USER\\Software\\Test","HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Run","HKEY_CURRENT_USER\\Software\\Policies\\Test","HKEY_CURRENT_USER\\Environment"})Check("reject REG key "+key,()=>Reject(()=>Policy.ParseRegistry(header+"["+key+"]\n\"a\"=\"b\"")));
foreach(var value in new[]{"-","hex:00,01","dword:wrong","\"bad\\q\"","\"a\"b\""})Check("reject REG value "+value,()=>Reject(()=>Policy.ParseRegistry(header+"[HKEY_CURRENT_USER\\Software\\Test]\n\"a\"="+value)));
Check("reject empty REG",()=>Reject(()=>Policy.ParseRegistry(header)));
Check("reject value without key",()=>Reject(()=>Policy.ParseRegistry(header+"\"a\"=\"b\"")));
Console.WriteLine($"{passed} checks passed; exit={Environment.ExitCode}");
