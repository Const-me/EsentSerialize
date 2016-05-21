In this folder, there're source files to build the documentation for this library.
To build the CHM:

1. Download and install HtmlHelpWorkshop from there:
http://www.microsoft.com/en-us/download/details.aspx?id=21138
the file named "htmlhelp.exe".
At the end of installation you might see a warning "a newer version found in c:\program files", ignore it.

2. Install SandCastle and SandcastleHelpBuilder using the guided installer from there:
http://shfb.codeplex.com/

3. Open BuildDoc.cmd from this folder, change the variables DXROOT, SCHBROOT and HHW to match your installation paths.
For HHW, specify the HHW you've just installed, the HHW in "C:\Program Files (x86)\HTML Help Workshop\" doesn't work.

4. Also in the BuildDoc.cmd, change DOCBUILDER variable to point to this DocBuilder location.

5. Modify the file "ReflectionToChmIndex.xsl" in the folder "%DXROOT%\ProductionTransforms".
Replace the line #40 thet starts from <xsl:template match="api[apidata/@group='member' ... with the following one:
<xsl:template match="api[apidata/@group='member' and not(topicdata/@notopic)]" > <!-- MODIFIED: Fix problem with missing properties and methods in index. -->

6. Build EsentSerialization project in the "Release" configuration.

7. Run BuildDoc.cmd

8. The EsentSerialization.chm should built.


The intermediate files are created in "EsentSerialization\Documentation" folder, which is safe to remove afterwards, and is listed in the .gitignore.
