using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
namespace HprGhost {
 public static class SteamGameDiscovery {
  static string Value(string text,string key){var match=Regex.Match(text,"\""+Regex.Escape(key)+"\"\\s*\"([^\"]*)\"",RegexOptions.IgnoreCase);return match.Success?match.Groups[1].Value.Replace("\\\\","\\"):"";}
  public static string Find(IEnumerable<string> steamRoots){var libraries=new List<string>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string root in steamRoots){if(string.IsNullOrWhiteSpace(root))continue;libraries.Add(root);try{string file=Path.Combine(root,"steamapps","libraryfolders.vdf");if(File.Exists(file))foreach(Match match in Regex.Matches(File.ReadAllText(file),"\"path\"\\s*\"([^\"]*)\"",RegexOptions.IgnoreCase))libraries.Add(match.Groups[1].Value.Replace("\\\\","\\"));}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}}
   foreach(string library in libraries){if(!seen.Add(library))continue;try{string manifest=Path.Combine(library,"steamapps","appmanifest_1328660.acf");if(!File.Exists(manifest))continue;string text=File.ReadAllText(manifest);if(Value(text,"appid")!="1328660")continue;string name=Value(text,"installdir");if(string.IsNullOrWhiteSpace(name)||name=="."||name==".."||name!=Path.GetFileName(name))continue;string folder=Path.Combine(library,"steamapps","common",name);if(GameLocation.Valid(folder))return Path.GetFullPath(folder);}catch(IOException){}catch(UnauthorizedAccessException){}catch(ArgumentException){}catch(NotSupportedException){}}
   return "";
  }
  public static string Resolve(){var roots=new List<string>();foreach(var item in new[]{new[]{@"HKEY_CURRENT_USER\Software\Valve\Steam","SteamPath"},new[]{@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam","InstallPath"},new[]{@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam","InstallPath"}})try{var value=Registry.GetValue(item[0],item[1],null) as string;if(value!=null)roots.Add(value);}catch(System.Security.SecurityException){}catch(UnauthorizedAccessException){}catch(IOException){}roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));return Find(roots);}
 }
}
