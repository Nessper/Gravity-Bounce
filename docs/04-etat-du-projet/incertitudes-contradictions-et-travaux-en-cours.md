# Incertitudes, contradictions et travaux en cours

> **Périmètre** : registre canonique des comportements non prouvés, divergences statiques et fichiers déjà en cours de modification.  
> **Statut** : chaque entrée reste ouverte faute de validation runtime ; aucune correction proposée.  
> **Date de vérification** : 2026-07-15.  
> **Principaux appuis** : comparaisons code/données/scènes, état Git initial, absence de Play Mode.

## Incertitudes fonctionnelles

| Domaine | Fait observé | Ce qui n’est pas établi |
|---|---|---|
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

Le chemin V2 du score est raccordé dans `Main` : source autoritaire `ScoreManager`, racine de transfert Screen Space Overlay, cible HUD, cadence d’absorption, impacts, odomètre mécanique et session visuelle d’accumulation. Le comportement a été validé en Play Mode pour les arrivées ordinaires, les rafales, les flushs rapprochés et le Fast Flush. Après cette validation, `ScoreImpactPacketUI.prefab`, `ScoreBinder`, `ScoreUI` et leurs branchements sérialisés ont été retirés. Les anciennes scènes d’archive qui conservaient encore `ScoreUI` ont également été supprimées ; ce nettoyage n’est plus un travail en cours.

Les familles drone K0, K1 et K2 sont également présentes dans les modifications locales : catalogue/localisation/icônes, socle commun, contrôleurs, exclusion temporaire des billes, assets visuels et câblage de `Main`. Les comportements principaux de K0/K1/K2 et la saisie-téléportation de K2 ont été validés de façon itérative en Play Mode par l’utilisateur ; les valeurs de mouvement, cooldown et effets restent des réglages de polish susceptibles d’évoluer avant commit.

Les valeurs de la famille I (`+15 %/+22 %/+30 %`) et ses prix `4/10/16` sont explicitement provisoires. Leur pipeline central et idempotent ainsi que ses tests Editor compilent, mais l’affichage et les totaux avec un module I réellement équipé restent à confirmer en Play Mode.

## Limites de l’analyse

Le chemin visuel du score décrit ci-dessus a été testé et validé en Play Mode par l'utilisateur. Les autres animations, UnityEvents dynamiques, ordres `Awake/Start`, valeurs par défaut de désérialisation, chargements d’assets et branches plateforme n’ont pas tous été observés systématiquement. Aucun `m_Script` explicitement nul n’a été retrouvé dans les scènes/prefabs, mais cette recherche ne remplace pas une validation exhaustive de chaque scène.

## Terminologie non établie

Les termes « scories » et « tokens » sont traités une seule fois dans le [guide de lecture et glossaire](../00-vue-ensemble/guide-de-lecture-et-glossaire.md), qui constitue leur référence canonique.
