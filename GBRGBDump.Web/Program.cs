using GBRGBDump.Web.Components;
using GBRGBDump.Web.Services.Impl;
using GBRGBDump.Web.Shared.Services;
using GBTools.Bootstrapper;
using GBTools.Common;
using GBTools.Decoder;
using GBTools.Graphics;
using Microsoft.Extensions.DependencyInjection;

namespace GBRGBDump.Web
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddRazorComponents()
            .AddInteractiveServerComponents();

            builder.Services.AddTransient<ImageTransformService>();
            builder.Services.AddTransient<IImportSavService, ImportSavService>();
            builder.Services.AddTransient<IFileReaderService, FileReaderService>();
            builder.Services.AddTransient<IFileWriterService, FileWriterService>();
            
            builder.Services.AddTransient<IApplyFrameService, ApplyFrameService>();
            builder.Services.AddTransient<ICharMapService, CharMapService>();
            builder.Services.AddTransient<ICompressAndHashService, CompressAndHashService>();
            builder.Services.AddTransient<IFileMetaService, FileMetaService>();
            builder.Services.AddTransient<ITransformImageService, TransformImageService>();
            builder.Services.AddTransient<IParseCustomMetadataService, ParseCustomMetadataService>();
            builder.Services.AddTransient<IRandomIdService, RandomIdService>();
            builder.Services.AddTransient<IImportSavService, ImportSavService>();
            builder.Services.AddTransient<IFrameDataService, FrameDataService>();

            builder.Services.AddTransient<IDecoderService, DecoderService>();
            builder.Services.AddTransient<IGameboyPrinterService, GameboyPrinterService>();

            builder.Services.AddTransient<IRgbImageProcessingService, RgbImageProcessingService>();

            // Web services
            builder.Services.AddTransient<IFileDialogService, FileDialogService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();
            app.UseAntiforgery();

            app.MapRazorComponents<App>()
                .AddInteractiveServerRenderMode();

            app.Run();
        }
    }
}
