pushd "%~dp0..\Source\"
@if %errorlevel% neq 0 exit /b %errorlevel%

@if exist ..\Distribution\ goto dirok
popd
@echo Distribution directory does not exist.
@exit /b 1

:dirok
copy Libraries\SM.Media\bin\Debug\SM.Media.dll ..\Distribution\bin\Debug\
copy Libraries\SM.Media\bin\Debug\SM.Media.pdb ..\Distribution\bin\Debug\
copy Libraries\SM.Media.Web.HttpClientReader\bin\Debug\SM.Media.Web.HttpClientReader.dll ..\Distribution\bin\Debug\
copy Libraries\SM.Media.Web.HttpClientReader\bin\Debug\SM.Media.Web.HttpClientReader.pdb ..\Distribution\bin\Debug\
copy Libraries\SM.Media\bin\Release\SM.Media.dll ..\Distribution\bin\Release\
copy Libraries\SM.Media\bin\Release\SM.Media.pdb ..\Distribution\bin\Release\
copy Libraries\SM.Media.Web.HttpClientReader\bin\Release\SM.Media.Web.HttpClientReader.dll ..\Distribution\bin\Release\
copy Libraries\SM.Media.Web.HttpClientReader\bin\Release\SM.Media.Web.HttpClientReader.pdb ..\Distribution\bin\Release\

copy Libraries\SM.Media.Platform.WP8\bin\Debug\SM.Media.Platform.WP8.dll ..\Distribution\bin\Debug\WP8\
copy Libraries\SM.Media.Platform.WP8\bin\Debug\SM.Media.Platform.WP8.pdb ..\Distribution\bin\Debug\WP8\
copy bin\Debug\SM.Media.Platform.WinRT.dll ..\Distribution\bin\Debug\WinRT\
copy bin\Debug\SM.Media.Platform.WinRT.pdb ..\Distribution\bin\Debug\WinRT\

copy Libraries\SM.Media.Platform.WP8\bin\Release\SM.Media.Platform.WP8.dll ..\Distribution\bin\Release\WP8\
copy Libraries\SM.Media.Platform.WP8\bin\Release\SM.Media.Platform.WP8.pdb ..\Distribution\bin\Release\WP8\
copy bin\Release\SM.Media.Platform.WinRT.dll ..\Distribution\bin\Release\WinRT\
copy bin\Release\SM.Media.Platform.WinRT.pdb ..\Distribution\bin\Release\WinRT\

copy Libraries\SM.Media.Builder\bin\Debug\SM.Media.Builder.dll ..\Distribution\bin\Debug\
copy Libraries\SM.Media.Builder\bin\Debug\SM.Media.Builder.pdb ..\Distribution\bin\Debug\
copy Libraries\SM.Media.Builder\bin\Release\SM.Media.Builder.dll ..\Distribution\bin\Release\
copy Libraries\SM.Media.Builder\bin\Release\SM.Media.Builder.pdb ..\Distribution\bin\Release\

copy Global\GlobalAssemblyInfo.cs ..\Distribution\Global\
copy Global\SM.MediaVersion.cs ..\Distribution\Global\
copy Global\readme.txt ..\Distribution\
copy Global\LICENSE.txt ..\Distribution\

copy Phone\HlsView.WP8\*.xaml ..\Distribution\Phone\HlsView.WP8\
copy Phone\HlsView.WP8\*.cs  ..\Distribution\Phone\HlsView.WP8\
copy Phone\HlsView.WP8\Assets\*.png  ..\Distribution\Phone\HlsView.WP8\Assets\
copy Phone\HlsView.WP8\Assets\Tiles\*.png  ..\Distribution\Phone\HlsView.WP8\Assets\Tiles\
copy Phone\HlsView.WP8\HlsView.WP8.csproj  ..\Distribution\Phone\HlsView.WP8\
copy Phone\HlsView.WP8\packages.config ..\Distribution\Phone\HlsView.WP8\
copy Phone\HlsView.WP8\Properties\*.cs ..\Distribution\Phone\HlsView.WP8\Properties\
copy Phone\HlsView.WP8\Properties\*.xml ..\Distribution\Phone\HlsView.WP8\Properties\
copy Phone\HlsView.WP8\Resources\*.cs ..\Distribution\Phone\HlsView.WP8\Resources\
copy Phone\HlsView.WP8\Resources\*.resx ..\Distribution\Phone\HlsView.WP8\Resources\

copy Libraries\SM.Media.MediaPlayer.Win81\*.config ..\Distribution\Libraries\SM.Media.MediaPlayer.Win81\
copy Libraries\SM.Media.MediaPlayer.Win81\*.cs ..\Distribution\Libraries\SM.Media.MediaPlayer.Win81\
copy Libraries\SM.Media.MediaPlayer.Win81\SM.Media.MediaPlayer.Win81.csproj ..\Distribution\Libraries\SM.Media.MediaPlayer.Win81\
copy Libraries\SM.Media.MediaPlayer.Win81\Properties\*.cs ..\Distribution\Libraries\SM.Media.MediaPlayer.Win81\Properties\

copy Libraries\SM.Media.MediaPlayer.WP8\*.config ..\Distribution\Libraries\SM.Media.MediaPlayer.WP8\
copy Libraries\SM.Media.MediaPlayer.WP8\*.cs ..\Distribution\Libraries\SM.Media.MediaPlayer.WP8\
copy Libraries\SM.Media.MediaPlayer.WP8\SM.Media.MediaPlayer.WP8.csproj ..\Distribution\Libraries\SM.Media.MediaPlayer.WP8\
copy Libraries\SM.Media.MediaPlayer.WP8\Properties\*.cs ..\Distribution\Libraries\SM.Media.MediaPlayer.WP8\Properties\

copy Libraries\SM.Media.MediaPlayer.WP81\*.config ..\Distribution\Libraries\SM.Media.MediaPlayer.WP81\
copy Libraries\SM.Media.MediaPlayer.WP81\*.cs ..\Distribution\Libraries\SM.Media.MediaPlayer.WP81\
copy Libraries\SM.Media.MediaPlayer.WP81\SM.Media.MediaPlayer.WP81.csproj ..\Distribution\Libraries\SM.Media.MediaPlayer.WP81\
copy Libraries\SM.Media.MediaPlayer.WP81\Properties\*.cs ..\Distribution\Libraries\SM.Media.MediaPlayer.WP81\Properties\

copy Libraries\SM.Media.BackgroundAudio.WP81\*.cs ..\Distribution\Libraries\SM.Media.BackgroundAudio.WP81\
copy Libraries\SM.Media.BackgroundAudio.WP81\SM.Media.BackgroundAudio.WP81.csproj ..\Distribution\Libraries\SM.Media.BackgroundAudio.WP81\
copy Libraries\SM.Media.BackgroundAudio.WP81\packages.config ..\Distribution\Libraries\SM.Media.BackgroundAudio.WP81\
copy Libraries\SM.Media.BackgroundAudio.WP81\Properties\*.cs ..\Distribution\Libraries\SM.Media.BackgroundAudio.WP81\Properties\

copy Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\packages.config ..\Distribution\Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\
copy Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\*.cs ..\Distribution\Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\
copy Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\SM.Media.BackgroundAudioStreamingAgent.WP8.csproj ..\Distribution\Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\
copy Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\Properties\*.cs ..\Distribution\Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\Properties\

copy Phone\SamplePlayer.WP8\*.xaml ..\Distribution\Phone\SamplePlayer.WP8\
copy Phone\SamplePlayer.WP8\*.cs  ..\Distribution\Phone\SamplePlayer.WP8\
copy Phone\SamplePlayer.WP8\Assets\*.png  ..\Distribution\Phone\SamplePlayer.WP8\Assets\
copy Phone\SamplePlayer.WP8\Assets\Tiles\*.png  ..\Distribution\Phone\SamplePlayer.WP8\Assets\Tiles\
copy Phone\SamplePlayer.WP8\SamplePlayer.WP8.csproj  ..\Distribution\Phone\SamplePlayer.WP8\
copy Phone\SamplePlayer.WP8\packages.config ..\Distribution\Phone\SamplePlayer.WP8\
copy Phone\SamplePlayer.WP8\Properties\*.cs ..\Distribution\Phone\SamplePlayer.WP8\Properties\
copy Phone\SamplePlayer.WP8\Properties\*.xml ..\Distribution\Phone\SamplePlayer.WP8\Properties\
copy Phone\SamplePlayer.WP8\Resources\*.cs ..\Distribution\Phone\SamplePlayer.WP8\Resources\
copy Phone\SamplePlayer.WP8\Resources\*.resx ..\Distribution\Phone\SamplePlayer.WP8\Resources\

copy Phone\BackgroundAudio.Sample.WP8\*.xaml ..\Distribution\Phone\BackgroundAudio.Sample.WP8\
copy Phone\BackgroundAudio.Sample.WP8\*.cs ..\Distribution\Phone\BackgroundAudio.Sample.WP8\
copy Phone\BackgroundAudio.Sample.WP8\Assets\*.png ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Assets\
copy Phone\BackgroundAudio.Sample.WP8\Assets\Tiles\*.png ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Assets\Tiles\
copy Phone\BackgroundAudio.Sample.WP8\BackgroundAudio.Sample.WP8.csproj ..\Distribution\Phone\BackgroundAudio.Sample.WP8\
copy Phone\BackgroundAudio.Sample.WP8\Images\*.png ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Images\
copy Phone\BackgroundAudio.Sample.WP8\packages.config ..\Distribution\Phone\BackgroundAudio.Sample.WP8\
copy Phone\BackgroundAudio.Sample.WP8\Properties\*.cs ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Properties\
copy Phone\BackgroundAudio.Sample.WP8\Properties\*.xml ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Properties\
copy Phone\BackgroundAudio.Sample.WP8\Resources\*.cs ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Resources\
copy Phone\BackgroundAudio.Sample.WP8\Resources\*.resx ..\Distribution\Phone\BackgroundAudio.Sample.WP8\Resources\

copy App\WinRT\BackgroundAudio.Sample.WP81\*.xaml ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\
copy App\WinRT\BackgroundAudio.Sample.WP81\*.cs ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\
copy App\WinRT\BackgroundAudio.Sample.WP81\BackgroundAudio.Sample.WP81.csproj ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\
copy App\WinRT\BackgroundAudio.Sample.WP81\Package.appxmanifest ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\
copy App\WinRT\BackgroundAudio.Sample.WP81\packages.config ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\
copy App\WinRT\BackgroundAudio.Sample.WP81\Assets\*.png ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\Assets\
copy App\WinRT\BackgroundAudio.Sample.WP81\Properties\*.cs ..\Distribution\App\WinRT\BackgroundAudio.Sample.WP81\Properties\

copy App\WinRT\HlsView.Win81\*.xaml ..\Distribution\App\WinRT\HlsView.Win81\
copy App\WinRT\HlsView.Win81\*.cs ..\Distribution\App\WinRT\HlsView.Win81\
copy App\WinRT\HlsView.Win81\HlsView.Win81.csproj ..\Distribution\App\WinRT\HlsView.Win81\
copy App\WinRT\HlsView.Win81\Assets\*.png ..\Distribution\App\WinRT\HlsView.Win81\Assets\
copy App\WinRT\HlsView.Win81\Properties\*.cs ..\Distribution\App\WinRT\HlsView.Win81\Properties\
copy App\WinRT\HlsView.Win81\HlsView.Win81_TemporaryKey.pfx ..\Distribution\App\WinRT\HlsView.Win81\
copy App\WinRT\HlsView.Win81\Package.appxmanifest ..\Distribution\App\WinRT\HlsView.Win81\
copy App\WinRT\HlsView.Win81\packages.config ..\Distribution\App\WinRT\HlsView.Win81\

copy App\WinRT\HlsView.WinRT.Shared\*.cs ..\Distribution\App\WinRT\HlsView.WinRT.Shared\
copy App\WinRT\HlsView.WinRT.Shared\HlsView.WinRT.Shared.projitems ..\Distribution\App\WinRT\HlsView.WinRT.Shared\
copy App\WinRT\HlsView.WinRT.Shared\HlsView.WinRT.Shared.shproj ..\Distribution\App\WinRT\HlsView.WinRT.Shared\

copy App\WinRT\HlsView.WP81\*.xaml ..\Distribution\App\WinRT\HlsView.WP81\
copy App\WinRT\HlsView.WP81\*.cs ..\Distribution\App\WinRT\HlsView.WP81\
copy App\WinRT\HlsView.WP81\HlsView.WP81.csproj ..\Distribution\App\WinRT\HlsView.WP81\
copy App\WinRT\HlsView.WP81\Assets\*.png ..\Distribution\App\WinRT\HlsView.WP81\Assets\
copy App\WinRT\HlsView.WP81\Properties\*.cs ..\Distribution\App\WinRT\HlsView.WP81\Properties\
copy App\WinRT\HlsView.WP81\Package.appxmanifest ..\Distribution\App\WinRT\HlsView.WP81\
copy App\WinRT\HlsView.WP81\app.config ..\Distribution\App\WinRT\HlsView.WP81\
copy App\WinRT\HlsView.WP81\packages.config ..\Distribution\App\WinRT\HlsView.WP81\

copy App\WinRT\SamplePlayer.Win81\*.xaml ..\Distribution\App\WinRT\SamplePlayer.Win81\
copy App\WinRT\SamplePlayer.Win81\*.cs ..\Distribution\App\WinRT\SamplePlayer.Win81\
copy App\WinRT\SamplePlayer.Win81\SamplePlayer.Win81.csproj ..\Distribution\App\WinRT\SamplePlayer.Win81\
copy App\WinRT\SamplePlayer.Win81\Assets\*.png ..\Distribution\App\WinRT\SamplePlayer.Win81\Assets\
copy App\WinRT\SamplePlayer.Win81\Properties\*.cs ..\Distribution\App\WinRT\SamplePlayer.Win81\Properties\
copy App\WinRT\SamplePlayer.Win81\SamplePlayer.Win81_TemporaryKey.pfx ..\Distribution\App\WinRT\SamplePlayer.Win81\
copy App\WinRT\SamplePlayer.Win81\Package.appxmanifest ..\Distribution\App\WinRT\SamplePlayer.Win81\
copy App\WinRT\SamplePlayer.Win81\packages.config ..\Distribution\App\WinRT\SamplePlayer.Win81\

copy App\WinRT\SamplePlayer.WinRT.Shared\*.xaml ..\Distribution\App\WinRT\SamplePlayer.WinRT.Shared\
copy App\WinRT\SamplePlayer.WinRT.Shared\*.cs ..\Distribution\App\WinRT\SamplePlayer.WinRT.Shared\
copy App\WinRT\SamplePlayer.WinRT.Shared\SamplePlayer.WinRT.Shared.projitems ..\Distribution\App\WinRT\SamplePlayer.WinRT.Shared\
copy App\WinRT\SamplePlayer.WinRT.Shared\SamplePlayer.WinRT.Shared.shproj ..\Distribution\App\WinRT\SamplePlayer.WinRT.Shared\

copy App\WinRT\SamplePlayer.WP81\*.xaml ..\Distribution\App\WinRT\SamplePlayer.WP81\
copy App\WinRT\SamplePlayer.WP81\*.cs ..\Distribution\App\WinRT\SamplePlayer.WP81\
copy App\WinRT\SamplePlayer.WP81\SamplePlayer.WP81.csproj ..\Distribution\App\WinRT\SamplePlayer.WP81\
copy App\WinRT\SamplePlayer.WP81\Assets\*.png ..\Distribution\App\WinRT\SamplePlayer.WP81\Assets\
copy App\WinRT\SamplePlayer.WP81\Properties\*.cs ..\Distribution\App\WinRT\SamplePlayer.WP81\Properties\
copy App\WinRT\SamplePlayer.WP81\Package.appxmanifest ..\Distribution\App\WinRT\SamplePlayer.WP81\
copy App\WinRT\SamplePlayer.WP81\app.config ..\Distribution\App\WinRT\SamplePlayer.WP81\
copy App\WinRT\SamplePlayer.WP81\packages.config ..\Distribution\App\WinRT\SamplePlayer.WP81\

copy .nuget\NuGet.config ..\Distribution\.nuget\

copy Sample*.sln ..\Distribution\

popd
@exit /b %errorlevel%
