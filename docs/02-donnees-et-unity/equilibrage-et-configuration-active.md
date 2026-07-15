# Équilibrage et configuration active

> **Périmètre** : valeurs structurantes confirmées et emplacement des réglages de contenu actifs.  
> **Statut** : valeurs observées le 2026-07-14 ; aucune appréciation d’équilibrage.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : SO sous `Assets/Project/Data`, JSON sous `Assets/Resources`, prefabs/scènes portant les réglages runtime.

## Balles

| ID | Points de base | Danger | Compte pour la progression |
|---|---:|---:|---:|
| `white` | 100 | non | oui |
| `blue` | 150 | non | oui |
| `red` | 200 | non | oui |
| `black` | -120 | oui | non |

Source canonique du projet : `Assets/Project/Data/Balls/Ball_*.asset`.

## Vaisseaux

| ID | Coque | Durée de base | Slots totaux | Slots ouverts | Argent initial | Visibilité |
|---|---:|---:|---:|---:|---:|---|
| `CORE_SCOUT` | 10 | 60 s | 6 | 3 | 4 | normal, débloqué par défaut |
| `AETHER_RUNNER` | 8 | 60 s | 5 | 3 | 6 | normal, non débloqué par défaut |
| `DEBUG_SHIP` | 10 | 10 s | 6 | 6 | 999 | caché, débloqué par défaut |

`DEBUG_SHIP` commence avec `HULL_PATCH_3`, `SCAN_ARRAY_T3` et `GREED_COIL_T2`. La divergence entre capacité et layouts d’`AETHER_RUNNER` est consignée uniquement dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Run et bacs

- Vies de contrat initiales : 3.
- Seuil automatique de flush de base : 5 balles, avant bonus GREED.
- Score, argent additionnel et inventaire sont réinitialisés lors d’un nouveau run ; l’argent initial vient du vaisseau.

## Récompenses de médailles

`EconomyConfig_Default.asset` définit bronze = 2, argent = 4 et or = 6. Ces valeurs sont consommées par les bonus de fin concernés ; elles ne sont pas les prix généraux des modules.

## Offres de boutique

- `W1_START` : 3 modules tier 1, poids 100.
- `W1_MID` : 2×T1 + 1×T2 (poids 80), 3×T1 (17), 1×T1 + 2×T2 (3).
- `W2_START` et `W2_MID` ont des règles définies, mais aucun monde W2 actif n’a été retrouvé.

Les prix, paramètres et effets détaillés des 27 modules résident dans `Assets/Resources/Modules/ModuleCatalog.json`. Le statut de travail observé de ce fichier est consigné dans [Incertitudes et travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Niveaux et combos

Les durées, quotas, mélanges, cibles, objectifs secondaires et obstacles sont définis niveau par niveau dans `Assets/Resources/Levels/W1-L*.json`. Les seuils/multiplicateurs de combos runtime sont dans `ComboDefinitionCatalog.asset`; les bonus finaux dans `FinalComboConfig_Default.asset`. Cette page ne recopie pas ces tables volumineuses afin d’éviter une seconde source de vérité documentaire.

Une valeur du combo final ne peut pas être établie depuis l’asset sérialisé observé ; le constat exact est centralisé dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).
