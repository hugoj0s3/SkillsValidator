using SkillsValidator.Engine.Abstractions.Repositories;
using SkillsValidator.Engine.AzureSpeech;
using SkillsValidator.Engine.AzureSpeech.IoC;
using SkillsValidator.Engine.IoC;
using SkillsValidator.Engine.OpenAI;
using SkillsValidator.Engine.OpenAI.IoC;
using SkillsValidator.Web;
using SkillsValidator.Web.Components;
using SkillsValidator.Web.Realtime;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var skillsFolder = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, builder.Configuration["SkillsFolder"] ?? "skills"));

// Keys come from user-secrets, the rest from appsettings.json.
var speechOptions = builder.Configuration.GetSection("AzureSpeech").Get<AzureSpeechOptions>() ?? new AzureSpeechOptions();
var openAIOptions = builder.Configuration.GetSection("OpenAI").Get<OpenAIOptions>() ?? new OpenAIOptions();

IocRegistration.AddSkillsValidator(builder.Services, skillsFolder);

// Providers: swap these lines to try other agents or speech services.
OpenAIRegistration.AddOpenAI(builder.Services, openAIOptions);
AzureSpeechRegistration.AddAzureSpeech(builder.Services, speechOptions);

var startupProblems = new StartupProblems();
builder.Services.AddSingleton(startupProblems);

var app = builder.Build();

// Check the skill configs and settings at startup, so problems are reported right away.
var loadResult = app.Services.GetRequiredService<ISkillConfigRepository>().GetLoadResult();
if (loadResult.HasErrors || loadResult.NoConfigFound)
    startupProblems.Messages.Add(ConfigErrorMessage.Build(loadResult, skillsFolder));

startupProblems.Messages.AddRange(speechOptions.Validate());
startupProblems.Messages.AddRange(openAIOptions.Validate());

if (startupProblems.Any)
    app.Logger.LogError("Please fix the following and re-run the app:{NewLine}{Problems}",
        Environment.NewLine, string.Join(Environment.NewLine, startupProblems.Messages));
else
    app.Logger.LogInformation("Loaded {Count} skill config(s) from {Folder}", loadResult.Configs.Count, skillsFolder);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseWebSockets();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// The interview voice: one WebSocket per interview (see InterviewVoiceEndpoint for the protocol).
InterviewVoiceEndpoint.Map(app);

app.Run();
