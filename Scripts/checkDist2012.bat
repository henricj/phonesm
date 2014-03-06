msbuild /m Samples.WP7.sln /t:Build "/p:Configuration=Release;Platform=Any CPU"
msbuild /m Samples.WP7.sln /t:Build "/p:Configuration=Debug;Platform=Any CPU"

msbuild /m Samples.WP8.sln /t:Build "/p:Configuration=Release;Platform=x86"
msbuild /m Samples.WP8.sln /t:Build "/p:Configuration=Release;Platform=ARM"
msbuild /m Samples.WP8.sln /t:Build "/p:Configuration=Debug;Platform=x86"
msbuild /m Samples.WP8.sln /t:Build "/p:Configuration=Debug;Platform=ARM"

msbuild /m Sample.Silverlight.sln /t:Build "/p:Configuration=Release;Platform=Any CPU"
msbuild /m Sample.Silverlight.sln /t:Build "/p:Configuration=Debug;Platform=Any CPU"

