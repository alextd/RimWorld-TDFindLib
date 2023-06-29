To add mod support for mod X:


Use Cookie Cutter to create project folder from template and handle all this:
- rename files: Template.csproj, ThingQueryTemplate.cs, Defs/TDFindLib_Template_ThingQueries.xml
- Def xml text (potentialy multiple defs with different names than X)
- AssemblyInfo.cs: AssemblyTitle, AssemblyProduct: TDFindLib_X
- csproj: RootNamespace, AssemblyName: TDFindLib_X
- cs: namespace and class

		
git add


Add created csproj to TDFindLib.sln
- maybe Add reference to Mod's DLL (copy local false)
- Compile
- Develop code, ezpz


git add


In main project, add packageId ( from X's About.xml <packageId> ):
Edit About-Release.xml, loadAfter: 
		<li>packageID</li>

Edit LoadFolders.xml:
		<li IfModActive="packageID">ModSupport/X</li>
		<li IfModActive="packageID">1.4/ModSupport/X</li>
		
git add
		
Edit news.xml
		Yup