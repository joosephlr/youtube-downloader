using System.Diagnostics;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using YtbDown.Models;
using YtbDown.Util;
using YoutubeExplode;
using YoutubeExplode.Videos.Streams;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.AspNetCore.Authentication;
using Google.Apis.YouTube.v3.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;
using Newtonsoft.Json;
using System.Collections.Generic;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.YouTube.v3;
using YoutubeExplode.Videos;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System.Text.RegularExpressions;

namespace YtbDown.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly YoutubeClient _youtube;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
            _youtube = new YoutubeClient();
        }

        public IActionResult Index()
        {
            // Verifica se o usuário já está autenticado
            if (!User.Identity.IsAuthenticated)
            {
                // Redireciona para o login do Google caso não esteja autenticado
                return RedirectToAction("Login", "Home");
            }

            return RedirectToAction("Login", "Home");
        }

        // Inicia o processo de login
        public IActionResult Login()
        {
            var redirectUrl = Url.Action("Callback", "Home");

            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };

            return Challenge(properties, "Google");
        }

        // Recebe a resposta do Google e processa o token de acesso
        public async Task<IActionResult> Callback()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync();

            if (!authenticateResult.Succeeded)
            {
                return BadRequest();  // Trate o erro de autenticação
            }

            // Pega os vídeos que foram retornados pelo Google
            //var videos = HttpContext.Items["youtube_videos"] as IList<Video>;
            var youtubeVideosJson = HttpContext.Session.GetString("youtube_videos");
            List<Google.Apis.YouTube.v3.Data.Video> videos = new List<Google.Apis.YouTube.v3.Data.Video>();

            if (!string.IsNullOrEmpty(youtubeVideosJson))
            {
                videos = JsonConvert.DeserializeObject<List<Google.Apis.YouTube.v3.Data.Video>>(youtubeVideosJson);
                // Agora você pode usar os vídeos na sua página
            }

            return View("Index", videos); // Passa os vídeos para a View
        }

        public async Task<IActionResult> GetVideoDetails(string videoId)
        {
            // Recupera o token de acesso do Google que foi armazenado na sessão
            var accessToken = HttpContext.Session.GetString("GoogleAccessToken");

            if (string.IsNullOrEmpty(accessToken))
            {
                return RedirectToAction("Login", "Home"); // Redireciona para login caso o usuário não tenha feito login
            }

            // Cria a credencial do Google a partir do token de acesso
            var googleCredential = GoogleCredential.FromAccessToken(accessToken);

            // Cria o serviço YouTube com a credencial
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = googleCredential,
                ApplicationName = "YouTube API Sample"
            });

            // Faz a requisição para buscar informações sobre o vídeo
            var request = youtubeService.Videos.List("snippet,contentDetails,statistics");
            request.Id = videoId; // Passa o ID do vídeo recebido como parâmetro

            var response = await request.ExecuteAsync();

            if (response.Items.Count == 0)
            {
                return NotFound("Vídeo não encontrado.");
            }

            // Retorna os detalhes do vídeo para a View
            var video = response.Items[0]; // Pega o primeiro item (único)
            return View(video);
        }

        [HttpPost]
        public async Task<IActionResult> BaixarArquivo(string tipoMedia, string mediaUrl)
        {
            try
            {
                // Recupera o token de acesso do Google que foi armazenado na sessão
                var accessToken = HttpContext.Session.GetString("GoogleAccessToken");

                if (string.IsNullOrEmpty(accessToken))
                {
                    return RedirectToAction("Login", "Home"); // Redireciona para login caso o usuário não tenha feito login
                }

                // Cria a credencial do Google a partir do token de acesso
                var googleCredential = GoogleCredential.FromAccessToken(accessToken);

                // Cria o serviço YouTube com a credencial
                var youtubeService = new YouTubeService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = googleCredential,
                    ApplicationName = "YouTube API Sample"
                });

                // Faz a requisição para buscar informações sobre o vídeo
                var request = youtubeService.Videos.List("snippet,contentDetails,statistics");

                // Define a expressão regular para capturar o ID do vídeo
                var pattern = @"(?:https?:\/\/(?:www\.)?youtube\.com\/(?:[^\/\n\s]+\/\S+\/|(?:v|e(?:mbed)?)\/|\S*?[?&]v=))([a-zA-Z0-9_-]{11})";

                // Cria a expressão regular
                var regex = new Regex(pattern);

                // Faz a correspondência da URL com a expressão regular
                var match = regex.Match(mediaUrl);

                // Verifica se encontrou o ID do vídeo
                if (match.Success)
                {
                    request.Id = match.Groups[1].Value; // Retorna o ID do vídeo
                }
                else
                {
                    request.Id = "";
                }

                var response = await request.ExecuteAsync();

                var videoInformacao = await _youtube.Videos.GetAsync(mediaUrl);
                var mediaTitulo = videoInformacao.Title;
                string diretorioDeSaida = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Downloads");

                mediaTitulo = FormNome.FormatarNomeArquivo(mediaTitulo, 100);
                string extensao = tipoMedia == "audio" ? "mp3" : "mp4";
                var diretorioSaida = Path.Combine(diretorioDeSaida, $"{mediaTitulo}.{extensao}");

                Directory.CreateDirectory(diretorioDeSaida);

                var videoStream = await _youtube.Videos.Streams.GetManifestAsync(match.Groups[1].Value);
                IStreamInfo infoFluxo = null;

                if (tipoMedia == "audio")
                {
                    infoFluxo = videoStream.GetAudioOnlyStreams().GetWithHighestBitrate();
                }
                else if (tipoMedia == "video")
                {
                    infoFluxo = videoStream.GetMuxedStreams().GetWithHighestVideoQuality();
                }

                if (infoFluxo != null)
                {
                    await _youtube.Videos.Streams.DownloadAsync(infoFluxo, diretorioSaida);
                }
                else
                {
                    throw new Exception(tipoMedia == "audio" ? "Nenhuma stream de áudio disponível." : "Nenhuma stream de vídeo disponível.");
                }

                string contentType = tipoMedia == "audio" ? "audio/mpeg" : "video/mp4";
                return PhysicalFile(diretorioSaida, contentType, Path.GetFileName(diretorioSaida));
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult About()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = System.Diagnostics.Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}