Put a copy of the playerframework source code here to build
the Player Framework-based WP7 examples.  The version currently
used for testing with phonesm is:

   https://playerframework.codeplex.com/SourceControl/changeset/f1bcdbb9f2f612f4ebf224eab98fcf57f923735b

Since the playerframework project used some NuGet packages older
than the packages used by phonesm, they were updated through the
Package Manager Console with:
   Update-Package -Project Microsoft.WP7.PlayerFramework.SL
