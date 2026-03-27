using Avalonia;
using Avalonia.Headless;
using Avalonia.Skia;

[assembly: AvaloniaTestApplication(typeof(DatasetStudio_HeadlessTests.TestAppBuilder))]

namespace DatasetStudio_HeadlessTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions
            {
                UseHeadlessDrawing = false
            });
    }
}
