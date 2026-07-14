# Analytics, crédits et services externes

> **Périmètre** : télémétrie alpha, formulaire de bug, crédits et liens externes.  
> **Statut** : appels et conditions confirmés statiquement ; aucune requête réseau envoyée.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `AlphaAnalytics.cs`, `BugReportHotkey.cs`, `CreditsController.cs`, `CreditsCatalog.json`, appels `Application.OpenURL`.

## Analytics alpha

`AlphaAnalytics` prépare des événements `level_end` et `run_end` et les envoie vers un formulaire Google. L’envoi est désactivé dans l’éditeur. Les données incluent des identifiants/contexte de run et des métriques de résultat selon l’événement. Une version d’analytics `0.1` est forcée par l’implémentation observée.

Le déclenchement du début de session analytics présente une divergence avec l’index du premier niveau normal. Le constat détaillé et sa limite sont centralisés dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Rapport de bug

`BugReportHotkey` écoute F8 et ouvre un formulaire Google de rapport. Cette action dépend du navigateur/OS et n’a pas été déclenchée pendant l’analyse.

## Crédits et liens

`CreditsController` charge `Resources/Credits/CreditsCatalog.json` pour construire l’écran. Le catalogue contient les entrées affichées. Un lien Discord est ouvert via l’API système depuis l’interface de crédits.

## Frontière de sécurité

Aucun service distant de sauvegarde, d’économie, de score autoritaire ou d’anti-triche n’a été retrouvé. Les services externes observés sont télémétriques ou ouvrent une page utilisateur ; le jeu reste local pour son état fonctionnel.
