using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.YouTube.v3;
using Google.Apis.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

// Configurar autentica��o OAuth 2.0 do Google
//builder.Services.AddAuthentication(options =>
//{
//    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
//})
//.AddCookie()

// Habilita a sess�o
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Tempo de expira��o da sess�o
    options.Cookie.HttpOnly = true; // Configura��es de seguran�a para o cookie da sess�o
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.Events.OnValidatePrincipal = async context =>
            {
                if (!context.Principal.HasClaim(c => c.Type == ClaimTypes.Name))
                {
                    var identity = (ClaimsIdentity)context.Principal.Identity;
                    identity.AddClaim(new Claim(ClaimTypes.Name, "Jose Lucas"));
                }
            };
        })


.AddOAuth("Google", options =>
{
    options.ClientId = builder.Configuration["Google:ClientId"];
    options.ClientSecret = builder.Configuration["Google:ClientSecret"];
    options.CallbackPath = new PathString("/signin-google");

    options.AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/auth";
    options.TokenEndpoint = "https://oauth2.googleapis.com/token";
    options.Scope.Add("https://www.googleapis.com/auth/youtube.readonly");

    options.SaveTokens = true;

    options.Events = new OAuthEvents
    {
        OnCreatingTicket = async context =>
        {
            // Aqui voc� pode salvar o token de acesso na sess�o, por exemplo
            var accessToken = context.AccessToken;
            context.HttpContext.Session.SetString("GoogleAccessToken", accessToken);

            // Cria a credencial de usu�rio com o token de acesso
            //var googleCredential = GoogleCredential.FromAccessToken(accessToken);

            //// Cria o servi�o YouTube com a credencial
            //var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            //{
            //    HttpClientInitializer = googleCredential,
            //    ApplicationName = "YouTube API Sample"
            //});

            //// Aqui voc� pode usar a API do YouTube com o token de acesso
            //var request = youtubeService.Videos.List("snippet,contentDetails,statistics");
            ////request.MyRating = YouTubeService.Videos.ListRequest.MyRatingEnum.Like; // Exemplo de busca
            //request.Id = "LUjn3RpkcKY";

            //var response = await request.ExecuteAsync();

            //// Armazena os v�deos obtidos na resposta
            ////context.HttpContext.Items["youtube_videos"] = response.Items;
            ////TempData["youtube_videos"] = JsonConvert.SerializeObject(response.Items);

            //context.HttpContext.Session.SetString("youtube_videos", JsonConvert.SerializeObject(response.Items));
        }
    };
});



// Adiciona o antiforgery
//builder.Services.AddAntiforgery(options =>
//{
//    options.Cookie.Name = "X-CSRF-TOKEN";
//});

// Add services to the container.
builder.Services.AddControllersWithViews();

var app = builder.Build();

//app.UseHttpsRedirection();
//app.UseStaticFiles();

//app.UseRouting();
app.UseSession(); // Habilita a sess�o
// Configura��o do pipeline de requisi��o
app.UseAuthentication();
app.UseAuthorization();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
