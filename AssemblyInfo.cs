using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("Embed a static website made of HTML, JS and CSS into an executable")]
[assembly: AssemblyProduct("web2exe")] // Avoid following characters: <>"|*
[assembly: AssemblyCopyright("Copyright (c) 2024 Julien Chebance")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyFileVersion("1.0.0.0")]

[assembly: SPAmode(true)] // Single Page Application mode: index.html will be served for every 404
[assembly: StartMaximized(false)]
[assembly: Kiosk(false)]
[assembly: MultipleInstance("unique")] // block: won't allow multiple instance and display an error ; unique: all files will be opened in one instance (filenames are sent through a websocket) ; allow: multiple instance are allowed
[assembly: Stateless(true)] // If false, the Browser profile will be created as a "[AssemblyProduct] data" folder in the same directory as the exe, and won't be deleted when exiting the app. Not recommended with MultipleInstance="allow"