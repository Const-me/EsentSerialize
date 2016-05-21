@echo off

cd ..

if exist Documentation rmdir /s /q Documentation
mkdir Documentation
cd Documentation

Set DXROOT=C:\Soft\Utils\SandCastle
Set SCHBROOT=C:\Soft\Utils\SandcastleHelpBuilder
Set HHW=C:\Soft\Utils\HtmlHelpWorkshop

Set DOCBUILDER=%~dp0

set PATH=%PATH%;%DXROOT%\ProductionTools;%SCHBROOT%

copy /y %DOCBUILDER%_project.xml .\project.xml

rem http://blogs.msdn.com/b/sandcastle/archive/2006/07/29/682398.aspx

copy ..\Core\EsentSerialize\bin\Release\EsentSerialize.XML .\comments.xml

Echo 4. run MRefBuilder
MrefBuilder.exe ..\Core\EsentSerialize\bin\Release\EsentSerialize.dll /out:.\reflection.org

Echo 5. Transform the output
XslTransform /xsl:"%DXROOT%\ProductionTransforms\ApplyVSDocModel.xsl" reflection.org /xsl:"%DXROOT%\ProductionTransforms\AddFriendlyFilenames.xsl" /out:reflection.xml

Echo 6. Generate a topic manifest
XslTransform /xsl:%DXROOT%\ProductionTransforms\ReflectionToManifest.xsl reflection.xml /out:manifest.xml

Echo 7. Create an output directory structure
call %DXROOT%\Presentation\vs2005\copyOutput.bat

Echo 8. Run BuildAssembler using the sandcastle component stack
BuildAssembler /config:%DOCBUILDER%sandcastle.config manifest.xml

Echo 8.5 Clean the shit after the 3-rd party SandCastleProjectBuilder components

copy /y %SCHBROOT%\Colorizer\CopyCode.gif .\Output\icons\CopyCode.gif
copy /y %SCHBROOT%\Colorizer\CopyCode_h.gif .\Output\icons\CopyCode_h.gif
rmdir /s /q .\Output\html\icons

move /y .\Output\html\scripts\highlight.js .\Output\scripts\highlight.js
rmdir /s /q .\Output\html\scripts

move /y .\Output\html\styles\highlight.css .\Output\styles\highlight.css
rmdir /s /q .\Output\html\styles
		
Echo 9. Generate HTML help project
XslTransform /xsl:%DOCBUILDER%ReflectionToChmProject.xsl reflection.xml /out:Output\test.hhp

Echo 10. Generate intermediate table of contents
XslTransform /xsl:%DXROOT%\ProductionTransforms\createvstoc.xsl reflection.xml /out:toc.xml

Echo 11. Generate HTML help project information
XslTransform /xsl:%DXROOT%\ProductionTransforms\TocToChmContents.xsl toc.xml /out:Output\test.hhc
XslTransform /xsl:%DXROOT%\ProductionTransforms\ReflectionToChmIndex.xsl reflection.xml /out:Output\test.hhk

Echo 12. Run HTML Help Compiler to generate Chm
%HHW%\hhc.exe output\test.hhp

copy /y output\EsentSerialization.chm ..\EsentSerialization.chm
pause
