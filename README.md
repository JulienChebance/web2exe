# ![Logo](favicon.ico) web2exe

Embed a static website made of HTML, JS and CSS into a Windows executable.

#### Features

- Small size: only a few dozen KB (depending on your website)
- Only two portable files to share - no need to install  
(The generated executable file and the LICENSE.txt file, see license below)
- No CORS checks
- Unlike solutions based on Electron or NW.js:
	- web2exe uses an existing installation of Microsoft Edge or Chrome browsers
	- **Thus, Edge or Chrome is required on the target computer to run this tool!**
	- Of course, no internet connection is required
- Support Single Page Applications (SPA)
- Support Kiosk mode
- Support web storage (cookies, LocalStorage and IndexedDB)
- No need of internet connection

#### Usage

- Create a new folder and copy the following files in it: ``AssemblyInfo.cs``, ``web2exe.cs``, ``LICENSE.txt`` and ``run.bat`` (See [Releases](https://github.com/JulienChebance/web2exe/releases/latest) to get a zip of these files)
- Put all the files from your website in it
	- The root page of your website must be ``index.html``
	- The icon must be ``favicon.ico``
- Edit the ``AssemblyInfo.cs`` file with your values
- Run ``run.bat``

The exe file will be created.

#### License

This Software is released under the [MIT](LICENSE.txt) license.

If you prefer not to distribute the ``LICENSE.txt`` file with the generated executable file, you may include the license notice in at least the main page (ie: index.html) of your embedded website, in a perfectly visible way for the end user (in the footer for example).