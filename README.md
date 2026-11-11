# SponsorPulse

**SponsorPulse** est une plateforme de gestion et d'analyse des activités de sponsoring, conçue pour aider les entreprises à optimiser leurs investissements sportifs et culturels.

## 🎯 Fonctionnalités Principales


### Analyse de Performance
- Tableau de bord des retours sur investissement (ROI)
- Indicateurs clés de performance (KPI) personnalisables


### Gestion des Actifs
- Accès aux médias via Cloudflare R2

## 📊 Stack Technique

- **Backend** : .NET 10, ASP.NET Core, Entity Framework Core
- **Frontend** : Blazor Server, LumexUI
- **Style** : Tailwind CSS v4
- **Base de données** : SQLite
- **Stockage** : Cloudflare R2

## 🔄 Déploiement Continu

Le déploiement est automatisé via GitHub Actions :

1. Une Pull Request est fusionnée sur `main`
2. Le build Tailwind et .NET s'exécutent
3. L'application est déployée automatiquement

## 📞 Contact

Pour toute question ou démonstration, veuillez ouvrir une issue sur le dépôt.

---

**License** : ISC
