using SharpSentry.Analysis;
using SharpSentry.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSingleton<CodeAnalyzer>();
builder.Services.AddHostedService<SentryWorker>();

var host = builder.Build();
host.Run();
