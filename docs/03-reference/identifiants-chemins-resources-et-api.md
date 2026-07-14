# Identifiants, chemins Resources et API

> **Périmètre** : identifiants stables observés, chemins de chargement et frontières d’API externes/locales.  
> **Statut** : confirmé par recherches de déclarations et consommateurs.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : catalogues `Assets/Resources`, enums/constantes C#, `SaveManager`, `AlphaAnalytics`, `CreditsController`.

## Chemins Resources canoniques

| Domaine | Chemin logique / fichier |
|---|---|
| Mondes | `Worlds/WorldCatalog` |
| Index niveaux | `Levels/LevelCatalog` |
| Niveau | `Levels/{levelId}` |
| Vaisseaux | `Ships/ShipCatalog` |
| Modules | `Modules/ModuleCatalog` |
| Règles boutique | `Shop/modules_shop_rules` |
| Localisation | `Localization/{domain}/{language}` |
| Crédits | `Credits/CreditsCatalog` |
| Style combo final | `UI/FinalComboStyleProvider` |
| Images vaisseaux | chemins `imagePath` du catalogue, sans extension |

Unity exige que ces chemins omettent extension et préfixe `Assets/Resources`.

## Identifiants de contenu

- Mondes : `W1`, `DBG`.
- Niveaux : `W1-L1` à `W1-L6`, `DBG-L1`.
- Tokens de plan : `SHOP:START`, `SHOP:MID`, `BOSS:{levelId}`, `END`.
- Vaisseaux : `CORE_SCOUT`, `AETHER_RUNNER`, `DEBUG_SHIP`.
- Balles : `white`, `blue`, `red`, `black`.
- Règles boutique : `W1_START`, `W1_MID`, `W2_START`, `W2_MID`.
- Domaines de localisation : `ui`, `ships`, `modules`, `dialogs`; langues présentes : `en`, `fr`.
- Combos runtime principaux : `WhiteStreak`, `BlueRush`, `RedStorm`, `FastFlush`, `WhiteChain`, `BlueChain`, `RedChain`, `Super`, `Ultra`, `Monster`.

Les 24 identifiants de module restent canoniques dans `ModuleCatalog.json`; les recopier ici créerait un second catalogue documentaire.

## Clés locales

- Sauvegarde JSON : `GameSave_v1`.
- Version ayant autorité sur la remise à zéro : `VS_GAME_VERSION`.

Les clés `VS_*` reflètent l’identité historique VoidScrappers. Leur sens actuel est déterminé par leurs consommateurs, pas par leur préfixe.

## API et sorties externes

- `PlayerPrefs` : stockage local de sauvegarde/version.
- `Resources.Load` : chargement des catalogues et assets runtime.
- `UnityWebRequest` : envoi de l’analytics alpha vers un formulaire Google.
- `Application.OpenURL` : formulaire de bug et lien Discord/crédits.
- `SceneManager` : navigation interne.

Aucune API distante de jeu autoritaire n’a été retrouvée. Les URL exactes ne sont pas reproduites ici afin que les scripts restent leur unique source de vérité.
