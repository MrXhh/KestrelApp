using KestrelApp.Telnet;
using KestrelApp.HttpProxy;
using KestrelApp.Transforms.SecurityProxy;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace KestrelApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            builder.Services
                .AddConnections()
                .AddHttproxy()
                .AddFlowAnalyze()
                .AddTlsDetect()
                .AddConnectionFactory();

            builder.Host.UseSerilog((hosting, logger) =>
            {
                logger.ReadFrom
                    .Configuration(hosting.Configuration)
                    .Enrich.FromLogContext().WriteTo.Console(outputTemplate: "{Timestamp:O} [{Level:u3}]{NewLine}{SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}");
            });

            builder.WebHost.ConfigureKestrel((context, kestrel) =>
            {
                var section = context.Configuration.GetSection("Kestrel");
                kestrel.Configure(section)
                    // ��ͨTelnet������,ʹ��telnet�ͻ��˾Ϳ��Խ���
                    .Endpoint("Telnet", endpoint => endpoint.ListenOptions.UseEcho())

                    // xor(α)���ܴ����Telnet������, telnet�ͻ��˲��ܽ���
                    .Endpoint("XorTelnet", endpoint => endpoint.ListenOptions.UseFlowXor().UseEcho())

                    // XorTelnet�����������telnet���ӵ��˷�����֮����������xor֮�����XorTelnet������������������TelnetЭ�鴦��
                    .Endpoint("XorTelnetProxy", endpoint => endpoint.ListenOptions.UseFlowXor().UseConnectionHandler<XorTelnetProxyHandler>())

                    // http������������ܴ����������ĳ���
                    .Endpoint("HttpProxy", endpoint => endpoint.ListenOptions.UseHttpProxy())

                    // http��https���˿�˫Э�������
                    .Endpoint("HttpHttps", endpoint => endpoint.ListenOptions.UseTlsDetect(option => { }));
            });

            var app = builder.Build();
            app.UseRouting();

            // http�����м�����ܴ���������http��������
            app.UseMiddleware<HttpProxyMiddleware>();

            // Telnet over WebSocket
            app.MapConnectionHandler<TelnetConnectionHandler>("/telnet");

            app.Map("/{**any}", async context => await context.Response.WriteAsync(nameof(KestrelApp)));
            app.Run();
        }
    }
}