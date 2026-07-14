# Guide de lecture et glossaire

> **Périmètre** : conventions documentaires et vocabulaire transversal.  
> **Statut** : canonique pour les niveaux de certitude et les termes.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : ensemble des scripts et données sous `Assets/Project` et `Assets/Resources`.

## Niveaux de certitude

- **Confirmé** : le comportement est directement exprimé par le code ou une valeur sérialisée et ses dépendances ont été retrouvées.
- **Observation statique** : le branchement est visible dans les fichiers Unity, mais son résultat exact dépend de l’exécution.
- **Incertain** : les sources sont ambiguës, contradictoires, incomplètes ou nécessitent une vérification en Play Mode.
- **Legacy** : élément encore présent, mais supplanté ou non retrouvé dans le chemin runtime principal.
- **Hybride** : plusieurs générations, formats ou mécanismes coexistent.
- **En cours** : fichiers modifiés ou non suivis déjà présents dans l’espace de travail au moment de l’analyse ; cela ne prouve pas leur état fonctionnel.

Une absence signifie « non retrouvée dans le périmètre analysé », et non « impossible dans tous les builds ». Les valeurs détaillées sont centralisées dans [Équilibrage et configuration active](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## Glossaire projet

| Terme | Sens observé |
|---|---|
| Run | Session composée de nœuds, avec argent, modules, coque, vies de contrat et score cumulés. |
| Nœud | Étape du plan de run : boutique, niveau, boss ou fin. |
| Coque / hull | Ressource du vaisseau endommagée notamment par les balles noires. |
| Vie de contrat | Droit de poursuivre le run après l’échec d’une mission ; distinct de la coque. |
| Bac / bin | Collecteur de balles ; son contenu forme un snapshot lors d’un flush. |
| Flush | Résolution et vidage d’un bac, avec transformations de modules, score et combos. |
| Progression de niveau | Compte de balles admissibles collectées vers l’objectif principal ; les noires en sont exclues. |
| Combo runtime | Combo évalué pendant les flushs (couleur, volume, timing, chaîne). |
| Combo final | Bonus évalué sur l’historique complet en fin de niveau. |
| Snapshot de fin | État sérialisable du résultat avant la cérémonie et le commit. |
| Token de fin | Identifiant technique rendant le traitement de fin idempotent. Ce n’est pas une monnaie. |
| Module | Objet de run acheté, possédé et éventuellement équipé dans un slot de vaisseau. |
| Tier | Niveau bronze, argent ou or d’un module. |
| Resources | Chargement Unity par chemin logique depuis `Assets/Resources`. |

## Termes non établis

Le terme « scorie » n’apparaît pas dans les sources analysées. Les balles noires constituent la mécanique négative la plus proche, mais la documentation ne les renomme pas. De même, aucune monnaie de gameplay nommée « token » n’a été retrouvée ; `EndLevelToken` relève de l’intégrité transactionnelle de fin de niveau. Cette section est la référence canonique pour ces deux termes absents.

## Règle de non-duplication

Chaque page système porte le détail de son domaine. Les pages d’architecture résument seulement les relations. Les tables de valeurs renvoient vers la page d’équilibrage, les chemins vers la référence des identifiants, et les ambiguïtés vers l’état du projet.
