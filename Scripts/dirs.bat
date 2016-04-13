pushd %~dp0..\
@if %errorlevel% neq 0 exit /b %errorlevel%

rmdir /s /q "%~dp0..\Distribution\"
@if %errorlevel% neq 0 goto errorexit

md Distribution\
@if %errorlevel% neq 0 goto errorexit

cd Distribution\
@if %errorlevel% neq 0 goto errorexit

md .nuget\

md App\
md App\WinRT\
md App\WinRT\BackgroundAudio.Sample.WP81\
md App\WinRT\BackgroundAudio.Sample.WP81\Assets\
md App\WinRT\BackgroundAudio.Sample.WP81\Properties\
md App\WinRT\HlsView.Win81\
md App\WinRT\HlsView.Win81\Assets\
md App\WinRT\HlsView.Win81\Properties\
md App\WinRT\HlsView.WinRT.Shared\
md App\WinRT\HlsView.WP81\
md App\WinRT\HlsView.WP81\Assets\
md App\WinRT\HlsView.WP81\Properties\
md App\WinRT\SamplePlayer.Win81\
md App\WinRT\SamplePlayer.Win81\Assets\
md App\WinRT\SamplePlayer.Win81\Properties\
md App\WinRT\SamplePlayer.WinRT.Shared\
md App\WinRT\SamplePlayer.WP81\
md App\WinRT\SamplePlayer.WP81\Assets\
md App\WinRT\SamplePlayer.WP81\Properties\

md bin\
md bin\Debug\
md bin\Release\
md bin\Debug\WP8\
md bin\Debug\WinRT\
md bin\Release\WP8\
md bin\Release\WinRT\

md Global\

md Libraries\
md Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\
md Libraries\SM.Media.BackgroundAudioStreamingAgent.WP8\Properties\
md Libraries\SM.Media.BackgroundAudio.WP81\
md Libraries\SM.Media.BackgroundAudio.WP81\Properties\
md Libraries\SM.Media.MediaPlayer.Win81\
md Libraries\SM.Media.MediaPlayer.Win81\Properties\
md Libraries\SM.Media.MediaPlayer.WP8\
md Libraries\SM.Media.MediaPlayer.WP8\Properties\
md Libraries\SM.Media.MediaPlayer.WP81\
md Libraries\SM.Media.MediaPlayer.WP81\Properties\

md Phone\
md Phone\BackgroundAudio.Sample.WP8\
md Phone\BackgroundAudio.Sample.WP8\Assets\
md Phone\BackgroundAudio.Sample.WP8\Assets\Tiles\
md Phone\BackgroundAudio.Sample.WP8\Images\
md Phone\BackgroundAudio.Sample.WP8\Properties\
md Phone\BackgroundAudio.Sample.WP8\Resources\
md Phone\HlsView\
md Phone\HlsView\Properties\
md Phone\HlsView.WP8\
md Phone\HlsView.WP8\Assets\
md Phone\HlsView.WP8\Assets\Tiles\
md Phone\HlsView.WP8\Properties\
md Phone\HlsView.WP8\Resources\
md Phone\SamplePlayer.WP8\
md Phone\SamplePlayer.WP8\Properties\
md Phone\SamplePlayer.WP8\Assets\
md Phone\SamplePlayer.WP8\Assets\Tiles\
md Phone\SamplePlayer.WP8\Resources\

popd
@exit /b %errorlevel%

:errorexit
popd
@exit /b 1
