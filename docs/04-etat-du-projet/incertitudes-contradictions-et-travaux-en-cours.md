# Incertitudes, contradictions et travaux en cours

> **Périmètre** : registre canonique des comportements non prouvés, divergences statiques et fichiers déjà en cours de modification.  
> **Statut** : chaque entrée reste ouverte faute de validation runtime ; aucune correction proposée.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : comparaisons code/données/scènes, état Git initial, absence de Play Mode.

## Incertitudes fonctionnelles

| Domaine | Fait observé | Ce qui n’est pas établi |
|---|---|---|
| Score | `ScoreManager.GetSnapshot` ajoute le total du snapshot et `FlushResolutionEngine` fournit un `FinalTotal` incluant le base score. | Un double comptage réel sur le chemin actif. |
| Combos runtime | Les états de chaîne/timing sont statiques ; resets retrouvés surtout en tutoriel/debug. | Leur remise à zéro entre deux niveaux normaux. |
| Combos finaux | L’évaluateur recherche `WhiteFlushChain` et équivalents, le runtime définit `WhiteChain` et équivalents. | Déclenchement effectif de ces bonus finaux. |
| Combo final | `ptsJustInTime` n’est pas sérialisé dans l’asset observé. | Valeur chargée par Unity. |
| Fin de plan | Une convention accepte `index == Count`; `EnsurePlanLoaded` borne à `Count - 1`. | État exact après le dernier nœud. |
| Récupération | `SaveManager` et `RunRecoveryOnBoot` traitent l’abandon/reprise avec des gardes. | Ordre exact des callbacks et absence absolue de double traitement. |
| Analytics | `BeginRun` exige l’index 0, mais le premier niveau normal suit la boutique d’index 0. | Création de la session analytics normale. |
| Vaisseaux | L’écran ne filtre pas clairement `isHidden`/déblocage et la sélection ajoute l’ID aux débloqués. | Règle produit réelle de déblocage et visibilité. |
| Aether Runner | `totalModuleSlots = 5`, mais six layouts existent. | Traitement/visibilité du layout d’index 5. |
| Input | Prefab paddle en mode delta ; composants cible monde souris/tactile présents. | Contrôle tactile réellement utilisé sur appareil. |
| DBG-L1 | Ancien champ `Mix.Type` au lieu de `BallId`. | Type réellement généré ; un fallback blanc est seulement plausible. |

## Champs sans consommation retrouvée

`LevelData.World`, `Title`, `LevelDurationSec`, `Lives`, anciens `Spawn`/`Balls`; angles minimum/maximum du spawner; `ObstaclePlacement.phaseIndex`; `RunConfig.SelectedWorld` et `CurrentLevelIndex`; `nodesCleared`, `profileId` et certains paramètres physiques tels que `maxBounceAngleDeg`. Leur présence est confirmée, leur effet actuel ne l’est pas.

## Travaux en cours observés avant documentation

L’espace de travail comportait déjà des modifications de polices TMP, `ComboDefinitionCatalog.asset`, `BallScore_TMP.prefab`, scènes `Main`/`DebugLauncher`, `ShipRuntimeSetup`, scripts d’overlays combo et `ModuleCatalog.json`.

Des fichiers non suivis ajoutaient : `CombosScoreRoot.prefab`, `ScoreImpactPacketUI.prefab`, `GameplayScoreImpactUI`, `ComboScoreUI`, `ScoreAttractorUI` et `ScoreFlushAbsorberUI`. Dans le YAML de Main observé, certaines références de cette nouvelle chaîne étaient nulles. La documentation les classe **en cours**, sans préjuger de leur destination ni de leur fonctionnement final.

## Limites de l’analyse

Unity n’a pas été ouvert et le Play Mode n’a pas été lancé. Les animations, UnityEvents dynamiques, ordres `Awake/Start`, valeurs par défaut de désérialisation, chargements d’assets et branches plateforme n’ont donc pas été observés en fonctionnement. Aucun `m_Script` explicitement nul n’a été retrouvé dans les scènes/prefabs, mais cette recherche ne remplace pas une importation Unity.

## Terminologie non établie

Les termes « scories » et « tokens » sont traités une seule fois dans le [guide de lecture et glossaire](../00-vue-ensemble/guide-de-lecture-et-glossaire.md), qui constitue leur référence canonique.
