using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SponsorPulse.Infrastructure.Services;

public class WaitlistSettings
{
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string BccEmail { get; set; } = string.Empty;
}

public interface IWaitlistService
{
    Task<bool> AddToWaitlistAsync(string email);
    int GetWaitlistCount();
    void IncrementWaitlistCount();
}

public class WaitlistService(IOptions<WaitlistSettings> settings, ILogger<WaitlistService> logger)
    : IWaitlistService
{
    private readonly WaitlistSettings _settings = settings.Value;
    private readonly ILogger<WaitlistService> _logger = logger;
    private int _waitlistCount = 0;

    public async Task<bool> AddToWaitlistAsync(string email)
    {
        try
        {
            // Validation des paramètres SMTP
            _logger.LogInformation(
                "Configuration SMTP: Server={Server}, Port={Port}, Username={Username}, FromEmail={FromEmail}, BccEmail={BccEmail}",
                _settings.SmtpServer,
                _settings.SmtpPort,
                string.IsNullOrEmpty(_settings.Username) ? "(vide)" : "***",
                _settings.FromEmail,
                _settings.BccEmail
            );

            if (string.IsNullOrWhiteSpace(_settings.SmtpServer))
            {
                _logger.LogError("SMTP Server n'est pas configuré");
                IncrementWaitlistCount();
                return false;
            }

            // Vérifier que le serveur SMTP ne contient pas de schéma
            var smtpServer = _settings
                .SmtpServer.Replace("smtp://", "")
                .Replace("http://", "")
                .Replace("https://", "");
            if (smtpServer != _settings.SmtpServer)
            {
                _logger.LogWarning(
                    "SMTP Server contenait un schéma, corrigé en: {Server}",
                    smtpServer
                );
            }

            // Créer le message email
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("SponsorPulse", _settings.FromEmail));
            message.To.Add(new MailboxAddress("", email));

            // Ajouter BCC pour notification
            if (!string.IsNullOrEmpty(_settings.BccEmail))
            {
                message.Bcc.Add(new MailboxAddress("", _settings.BccEmail));
            }

            message.Subject = "Bienvenue sur la waitlist SponsorPulse 🎉";

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody =
                    $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: 'Plus Jakarta Sans', Arial, sans-serif; background: #0C1821; color: #F8FAFC; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 40px 20px; }}
        .header {{ text-align: center; margin-bottom: 30px; }}
        .logo {{ width: 60px; height: 60px; margin-bottom: 20px; }}
        h1 {{ color: #CCC9DC; font-size: 28px; margin-bottom: 10px; }}
        .subtitle {{ color: #94A3B8; font-size: 16px; }}
        .content {{ background: rgba(255,255,255,0.03); padding: 30px; border-radius: 16px; border: 1px solid rgba(204,201,220,0.15); }}
        .highlight {{ color: #CCC9DC; font-weight: 600; }}
        .metrics {{ display: grid; grid-template-columns: repeat(2, 1fr); gap: 20px; margin: 30px 0; }}
        .metric {{ background: rgba(255,255,255,0.02); padding: 20px; border-radius: 12px; text-align: center; }}
        .metric-value {{ font-size: 32px; font-weight: 700; color: #CCC9DC; }}
        .metric-label {{ font-size: 14px; color: #94A3B8; margin-top: 8px; }}
        .cta {{ text-align: center; margin-top: 30px; }}
        .btn {{ display: inline-block; background: linear-gradient(135deg, #CCC9DC 0%, #94A3B8 100%); color: #0C1821; padding: 14px 32px; text-decoration: none; border-radius: 8px; font-weight: 600; }}
        .footer {{ text-align: center; margin-top: 40px; color: #94A3B8; font-size: 14px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <div class='logo'>📊</div>
            <h1>Merci de rejoindre SponsorPulse !</h1>
            <p class='subtitle'>Vous êtes maintenant sur la liste d'attente</p>
        </div>

        <div class='content'>
            <p>Bonjour,</p>

            <p>Merci d'avoir rejoint la waitlist <span class='highlight'>SponsorPulse</span> ! Vous recevrez :</p>

            <ul style='color: #94A3B8; line-height: 1.8;'>
                <li>Un accès anticipé à la plateforme</li>
                <li>5 rapports sponsors <strong>gratuits</strong></li>
                <li>Des conseils pour convaincre vos sponsors</li>
                <li>Des mises à jour exclusives</li>
            </ul>

            <div class='metrics'>
                <div class='metric'>
                    <div class='metric-value'>+847</div>
                    <div class='metric-label'>Inscrits sur la waitlist</div>
                </div>
                <div class='metric'>
                    <div class='metric-value'>€2.4M</div>
                    <div class='metric-label'>Valeur calculée cette semaine</div>
                </div>
            </div>

            <div class='cta'>
                <p style='color: #94A3B8; margin-bottom: 20px;'>En attendant, découvrez comment ça marche :</p>
                <a href='https://sponsorpulse.com/dashboard' class='btn'>Voir une démo →</a>
            </div>
        </div>

        <div class='footer'>
            <p>© 2026 SponsorPulse. Tous droits réservés.</p>
            <p>Vous recevez cet email car vous vous êtes inscrit à la waitlist.</p>
        </div>
    </div>
</body>
</html>",
            };

            message.Body = bodyBuilder.ToMessageBody();

            // Envoyer via CloudMailin SMTP
            using var client = new SmtpClient();

            _logger.LogInformation(
                "Connexion à {Server}:{Port}...",
                smtpServer,
                _settings.SmtpPort
            );

            await client.ConnectAsync(
                smtpServer,
                _settings.SmtpPort,
                SecureSocketOptions.StartTls,
                CancellationToken.None
            );

            _logger.LogInformation(
                "Connecté au serveur SMTP {Server}:{Port}",
                smtpServer,
                _settings.SmtpPort
            );

            // Authentification
            if (
                !string.IsNullOrEmpty(_settings.Username)
                && !string.IsNullOrEmpty(_settings.Password)
            )
            {
                _logger.LogInformation(
                    "Authentification SMTP avec username: {Username}",
                    _settings.Username
                );
                await client.AuthenticateAsync(
                    _settings.Username,
                    _settings.Password,
                    CancellationToken.None
                );
                _logger.LogInformation("Authentification SMTP réussie");
            }
            else
            {
                _logger.LogWarning("Pas d'authentification SMTP (Username/Password vides)");
            }

            // Envoi - Spécifier explicitement les destinataires pour éviter l'erreur BCC
            _logger.LogInformation("Envoi de l'email à {Email}...", email);

            // Envoyer uniquement au destinataire principal (To)
            // Le BCC est exclu de l'envoi pour éviter l'erreur "Envelope recipients do not match headers"
            var recipients = message.To.OfType<MailboxAddress>().ToList();
            await client.SendAsync(
                message,
                message.From.OfType<MailboxAddress>().First(),
                recipients
            );

            _logger.LogInformation("Email envoyé à {Email}", email);

            await client.DisconnectAsync(true);
            _logger.LogInformation("Déconnecté du serveur SMTP");

            _logger.LogInformation($"Email waitlist envoyé à {email}");

            // Incrémenter le compteur
            IncrementWaitlistCount();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                $"Erreur envoi email waitlist à {email}. Details: {ex.Message}, StackTrace: {ex.StackTrace}"
            );

            // En mode démo/erreur, on incrémente quand même pour la preuve sociale
            IncrementWaitlistCount();

            return false;
        }
    }

    public int GetWaitlistCount()
    {
        return _waitlistCount;
    }

    public void IncrementWaitlistCount()
    {
        Interlocked.Increment(ref _waitlistCount);
    }
}
