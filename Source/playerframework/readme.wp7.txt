Put a copy of the playerframework source code here to build
the Player Framework-based WP7 examples.  The version currently
used for testing with phonesm is (on the "universal" branch):

   https://playerframework.codeplex.com/SourceControl/changeset/9f8f37c22b8d40026b6e9b0b5ac2128ce214b743

Since the playerframework project used some NuGet packages older
than the packages used by phonesm, they were updated through the
Package Manager Console with:
   Update-Package -Project Microsoft.WP7.PlayerFramework.SL
