# Contexte IA du projet 404

> **Usage** : porte d’entrée autonome pour un agent IA sans accès initial au dépôt.  
> **Statut** : synthèse de l’état observé par analyse statique ; aucun Play Mode, build ou test runtime.  
> **Dernière vérification** : 2026-07-14.  
> **Sources principales** : les 28 documents sous `docs/`, scènes de build, catalogues `Resources`, ScriptableObjects structurants et état Git observé.

## 1. Présentation factuelle

404, dont le nom Player est `404 - A Space Arcade Roguelite`, est un projet Unity 2D construit avec Unity `6000.2.6f2` et URP `17.2.0`. Le joueur sélectionne un vaisseau, parcourt une run composée de boutiques et de missions, contrôle un paddle, collecte des balles dans des bacs et résout ces bacs par des « flushs ». Les flushs alimentent score, progression de mission, combos et effets de modules. Les balles noires constituent un danger : elles ne font pas progresser l’objectif et peuvent endommager la coque lorsqu’elles ne sont pas transformées.

Le projet est local : sauvegarde, score, économie et progression ne reposent sur aucun backend d’autorité retrouvé. Les sorties réseau observées servent à l’analytics alpha ou ouvrent des formulaires/liens externes.

## 2. État actuel et niveau de certitude

L’architecture décrite est celle des sources et assets présents le 2026-07-14. Le projet contient un chemin runtime actuel, plusieurs couches historiques encore présentes et une intégration de présentation du score en cours dans l’espace de travail.

Les termes suivants ont un sens strict :

- **Confirmé** : exprimé directement par le code ou les données et relié à un consommateur retrouvé.
- **Observation statique** : visible dans les scènes/prefabs ou le code, mais résultat exact dépendant de l’exécution.
- **Incertain** : sources ambiguës, contradictoires ou nécessitant Play Mode/appareil.
- **Hybride** : plusieurs générations, formats ou mécanismes coexistent.
- **Hérité / legacy** : présent, mais supplanté ou non retrouvé sur le chemin principal.
- **En cours** : fichiers locaux modifiés/non suivis ; cela ne prouve pas leur fonctionnement final.

Unity n’a pas été lancé pour produire l’instantané documentaire. L’ordre exact des callbacks, les UnityEvents dynamiques, les animations, les valeurs par défaut de désérialisation et les branches plateforme restent donc hors validation runtime.

## 3. Architecture globale

404 est un monolithe Unity sans `.asmdef` propre au gameplay. Les scripts runtime compilent principalement dans `Assembly-CSharp`. L’architecture combine :

| Couche | Responsabilité | Points d’entrée structurants |
|---|---|---|
| Boot persistant | Installer les services globaux et survivre aux changements de scène | `Boot`, `Bootstrapper`, `BootRoot` |
| Navigation | Charger les écrans et missions | `GameFlowController` |
| Persistance | Profil, run, marqueurs de reprise, snapshot/token de fin | `SaveManager`, `GameSaveData` |
| État de run | Miroir runtime observable, plan et ressources | `RunSessionState`, `RunPlan`, `RunNavigator` |
| Catalogues | Résoudre mondes, niveaux, vaisseaux et modules | services `*CatalogService`, JSON `Resources` |
| Mission | Construire le contexte et orchestrer le niveau | `LevelBootstrapper`, `LevelManager` |
| Résolution | Spawn, collecte, flush, score, combos, objectifs, coque | `BallSpawner`, `BinCollector`, `FlushResolutionEngine`, `ScoreManager` |
| Présentation | HUD, overlays, narration, audio et FX | `MainUIController`, `AudioManager`, scripts `UI`/`FX` |

Le root de `Boot` est normalement requis par toutes les scènes suivantes. `MainStandaloneInstaller` et `MainDebugStarterV3` fournissent des chemins alternatifs pour exécuter `Main` seule ou avec un contexte debug.

## 4. Flux principal

```text
Boot → Title → ShipSelect → RunHub → Main
                              ↑       │
                              └───────┘ entre les nœuds
                                      └→ destination de fin / CreditsScene

DebugLauncher → Main  (chemin de test)
```

1. **Boot** installe sauvegarde, flow, audio, localisation, analytics, récupération, configurations physiques/plateforme/combo et services de modules.
2. **Title** expose l’entrée du parcours.
3. **ShipSelect** choisit un identifiant de vaisseau et initialise une nouvelle run.
4. **RunHub** affiche le nœud courant. Une boutique reste dans le hub ; une mission charge `Main`.
5. **Main** résout le `LevelContext`, déroule briefing, dialogue, assemblage du plateau, compte à rebours, tutoriel éventuel, timer et spawn.
6. Pendant le jeu, le paddle renvoie les balles, les bacs les collectent et les flushs déclenchent transformations, score, progression, combos et dégâts.
7. La **fin de niveau** arrête le spawn, évacue, force un flush final, nettoie les balles, évalue objectifs/combos/bonus et crée un snapshot associé à un token.
8. La cérémonie et l’overlay final présentent ce snapshot. Le commit applique le résultat une seule fois au run.
9. Le jeu revient au hub pour le nœud suivant, ou atteint la destination de fin de run.

Sept scènes sont activées au build : `Boot`, `Title`, `ShipSelect`, `RunHub`, `Main`, `DebugLauncher`, `CreditsScene`. Les anciennes copies hors build `MainScene_tmp.unity` et `_Recovery/0.unity` ont été supprimées.

## 5. Boucle d’une run

Une nouvelle run reçoit un ID, le vaisseau choisi, sa coque, trois vies de contrat, son argent initial, un score nul, inventaire/équipement initial et un plan de nœuds. Pour W1, le plan observé est :

```text
SHOP:START → W1-L1 → W1-L2 → W1-L3 → SHOP:MID
           → W1-L4 → W1-L5 → BOSS:W1-L6 → END
```

La run transporte d’un nœud au suivant la coque, les vies de contrat, l’argent, le score, les modules, les slots, les offres de boutique et les marqueurs de progression. Une défaite peut consommer une vie de contrat. La coque et les vies de contrat sont deux ressources différentes. Argent et modules sont propres à la run ; ils ne constituent pas une progression permanente.

## 6. Sources de vérité

L’ordre suivant évite de confondre définition, état runtime et présentation :

| Type | Autorité observée | Exemples |
|---|---|---|
| Sauvegarde | JSON `GameSaveData` dans `PlayerPrefs`, géré par `SaveManager` | profil, run, offres, snapshot/token |
| JSON sous Resources | contenu statique résolu par identifiant | mondes, niveaux, vaisseaux, modules, boutique, localisation, crédits |
| ScriptableObjects | définitions/configurations et un état runtime | balles, combos, économie, combos finaux, équipage, `RunSessionState` |
| Scènes/prefabs | câblage, références, valeurs sérialisées, composition visuelle | services Boot, managers Main, HUD, overlays |
| Code | orchestration, règles, fallbacks et constantes | trois vies de contrat, pipelines de résolution, clés PlayerPrefs |
| État runtime | données calculées non persistantes | `LevelContext`, contenu physique, timers, états de présentation |

Une valeur présente dans un asset ne devient pas automatiquement active : il faut retrouver son consommateur et son branchement. Inversement, une référence de scène peut être remplacée ou injectée au runtime.

## 7. Résumé des grands systèmes

| Système | État et rôle actuel |
|---|---|
| Démarrage/navigation | Actif. Root persistant au Boot et navigation centralisée par `GameFlowController`. |
| Sauvegarde/reprise | Actif et local. JSON PlayerPrefs, marqueurs de niveau, snapshot/token de fin. |
| Progression permanente | Limitée : meilleur score, tutoriel, sélection/liste de vaisseaux. Déblocage réel des vaisseaux incertain. |
| Run/nœuds | Actif. Plan construit depuis `WorldCatalog`, hub entre boutiques et missions. |
| Vaisseaux | Actif, trois définitions dont un vaisseau debug caché. Divergence de slots sur Aether Runner. |
| Coque/vies | Actif. Deux ressources séparées ; les noires affectent la coque, les échecs peuvent consommer une vie de contrat. |
| Économie | Active, monnaie uniquement liée à la run. Achat, réparation et reroll. |
| Modules | Actif, 24 modules, huit familles, inventaire et équipement persistés pendant la run. Catalogue local en cours de modification dans l’instantané Git. |
| Niveaux/spawn | Actif. W1-L1 à W1-L6 plus DBG-L1, phases, quotas, mélange, pooling et spawns forcés. |
| Balles/physique | Actif. Blanches, bleues, rouges positives ; noires dangereuses. Physique 2D et rebond personnalisé. |
| Bacs/flush | Actif. Seuil automatique de base 5, transformations de modules, score puis conséquences des noires. |
| Score/progression | Actif, mais une ambiguïté de calcul du score de base est documentée. Les noires ne progressent pas l’objectif. |
| Objectifs/combos | Actif. Objectif principal, secondaires, combos runtime et bonus finaux ; plusieurs incertitudes sur états/IDs finaux. |
| Fin de niveau | Chemin actuel actif : évacuation, nettoyage, snapshot, cérémonie, `EndResult`, commit idempotent. Anciennes générations encore présentes. |
| Tutoriel/pause/input | Actif mais input hybride. Tutoriel W1-L1 persistant ; voies clavier/souris/tactile ; nouveau Input System peu consommé. |
| UI/HUD | Actif et fortement câblé dans les scènes. Nouvelle chaîne visuelle de score en cours dans l’espace de travail. |
| Audio/localisation | Actif. Manager global, musiques de niveau, SFX, dialogues pondérés ; localisation hybride et langue Boot fixée à `fr`. |
| Caméra/FX/vidéo | Actif pour la présentation ; ne possède pas l’état métier. URP, parallax, menace noire, impacts, post-traitement, vidéo titre. |
| Analytics/externe | Alpha. Événements vers formulaire Google hors Editor, bug report F8, Discord/crédits. Aucun backend de jeu. |
| Debug | Présent et partiellement actif dans Main. Launcher, installateur standalone, testeurs/loggers et outils Editor. |

La référence condensée par domaine est [AI_SYSTEMS_REFERENCE_404.md](AI_SYSTEMS_REFERENCE_404.md).

## 8. Relations essentielles

- `Boot` rend disponibles `SaveManager`, `RunSessionState`, catalogues, audio, localisation et autres services aux scènes suivantes.
- `WorldCatalog` → `RunPlanBuilder` → `RunSessionState` détermine quel nœud le hub ou `Main` doit traiter.
- `ShipCatalog` initialise coque, durée, argent et slots ; les modules équipés peuvent modifier certains paramètres.
- JSON de niveau + run + modules → `LevelContext` → timer, spawner, objectifs, briefing et obstacles.
- `BallSpawner` → physique → `BinCollector` → `BinSnapshot` → modules/`FlushResolutionEngine` → `ScoreManager`, `HullSystem`, combos et UI.
- Score/historique/objectifs → évaluateurs de fin → `EndLevelOutcome` → `EndLevelSnapshot` → cérémonie → commit du run.
- `RunSessionState`, `ScoreManager`, `HullSystem`, timer et bacs alimentent le HUD via événements, UnityEvents, appels directs et binders.

## 9. Persistance et temporalité

Les données se répartissent en trois durées de vie distinctes ; cette séparation est essentielle pour raisonner sur sauvegarde et effets de bord.

### Permanent entre les runs

- `profileId` présent mais rôle fonctionnel incomplet ;
- vaisseau sélectionné et liste de vaisseaux débloqués ;
- meilleur score de run ;
- complétion du tutoriel.

### Persisté pendant une run

- ID de run, monde/plan/nœud, vaisseau ;
- coque, vies de contrat, score, argent ;
- modules possédés/équipés, slots et bonus de coque ;
- offres de boutique et rerolls ;
- marqueurs de niveau en cours/abandonné ;
- éventuels token et snapshot de fin en attente.

### Temporaire à une mission ou une scène

- contexte calculé, objets physiques, contenu vivant des bacs ;
- timer, quotas courants, obstacles instanciés ;
- animations, overlays et feedbacks ;
- états de combo runtime, avec une incertitude sur leur reset entre niveaux.

## 10. Actif, hybride, hérité et en cours

- **Chemin actif de fin** : `ResultsCeremonyOverlayController` puis `EndResultOverlayController`, avec `EndLevelOutcomeBuilder` et snapshot/token.
- **Fin héritée/hybride** : familles `VictoryUI`/`DefeatGameOverUI`/`LevelScoreSummaryUI`, puis `EndLevelUI`/`FinalPanelUI`, encore présentes dans le projet.
- **Transition actuelle** : `MainExitTransitionController`; `NextTransitionController` représente une génération antérieure.
- **Identité hybride** : nom 404 mais namespaces, clés `VS_*`, menus et identifiants historiques VoidScrappers.
- **Données hybrides** : ancien schéma de niveau encore visible, notamment DBG-L1.
- **Input hybride** : ancien Input Manager dominant, nouveau Input System installé/activé.
- **Localisation hybride** : packs en/fr, langue Boot fixée à fr, textes directs et fallbacks codés.
- **En cours** : présentation du score et catalogue de modules dans l’espace de travail observé.
- **Debug présent dans Main** : plusieurs testeurs/loggers attachés à des GameObjects actifs, exécution exacte dépendante de leurs drapeaux.

## 11. Principales incertitudes

Ces points sont des constats ouverts, pas des bugs affirmés :

1. intégration possible du score de base à deux endroits ;
2. reset des états statiques de combo entre niveaux normaux non retrouvé ;
3. IDs recherchés par certains combos finaux différents des IDs runtime ;
4. valeur `ptsJustInTime` absente de l’asset sérialisé observé ;
5. convention de fin de plan (`Count`) opposée au bornage à `Count - 1` ;
6. ordre exact des deux mécanismes de récupération au Boot ;
7. démarrage analytics conditionné à un index qui correspond normalement à la boutique ;
8. visibilité/déblocage des vaisseaux et auto-ajout lors de la sélection ;
9. Aether Runner : cinq slots déclarés, six layouts ;
10. voie tactile réelle avec paddle en mode delta ;
11. ancien champ `Mix.Type` de DBG-L1 face au `BallId` courant.

## 12. Risques à connaître avant modification

« Risque » signifie ici zone de propagation ou de validation, sans présumer d’un défaut :

- une modification de sauvegarde touche données persistées, migration par version, récupération et miroir `RunSessionState` ;
- une modification de run peut affecter hub, navigation, analytics et commit de fin ;
- un changement de module peut affecter boutique, inventaire, équipement, briefing, durée, flush, score, coque ou récompense ;
- le score est consommé par HUD, objectifs, combos, snapshot, cérémonie, analytics et meilleur score ;
- scènes/prefabs portent des références sérialisées avec GUID/fileID invisibles dans une lecture C# seule ;
- les services persistants et l’ordre `Awake`/`Start` ne peuvent pas être déduits complètement sans exécution ;
- `RunSessionState.asset` est un ScriptableObject mutable runtime : distinguer valeur sérialisée d’éditeur et état synchronisé ;
- les événements statiques de combos peuvent traverser les frontières de scène ;
- `PlayerPrefs.DeleteAll()` est appelé lors d’un changement de version applicative ;
- les chemins debug peuvent muter l’état local et ne constituent pas une protection anti-triche ;
- plusieurs fichiers Unity et de score sont déjà modifiés : toute analyse future doit préserver ces changements locaux.

Le protocole de travail recommandé est dans [AI_WORKING_GUIDE_404.md](AI_WORKING_GUIDE_404.md).

## 13. Glossaire

| Terme | Sens dans 404 |
|---|---|
| Run | Parcours persistant temporaire composé de nœuds, avec ressources et score cumulés. |
| Nœud / node | Boutique, niveau, boss ou fin dans le plan de run. |
| Coque / hull | Santé du vaisseau pendant la run. |
| Vie de contrat | Droit distinct de la coque, consommable après échec de mission. |
| Bac / bin | Collecteur de balles produisant un snapshot au flush. |
| Flush | Résolution/vidage d’un bac : transformations, score, combos, dégâts, recyclage. |
| Progression | Compte de balles admissibles vers l’objectif principal ; les noires sont exclues. |
| Combo runtime | Combo évalué pendant les flushs. |
| Combo final | Bonus évalué sur l’historique complet après nettoyage final. |
| Snapshot de fin | Résultat sérialisable présenté et récupérable après interruption. |
| Token de fin | Identifiant transactionnel anti-double-commit ; ce n’est pas une monnaie. |
| Module | Objet de run acheté, possédé puis éventuellement équipé dans un slot. |
| Tier | Bronze, argent ou or. |
| Resources | Système Unity de chargement par chemin logique sous `Assets/Resources`. |

« Scorie » n’apparaît pas dans les sources analysées. Il ne faut pas l’assimiler automatiquement aux balles noires. Aucune monnaie nommée « token » n’a été retrouvée.

## 14. Documents canoniques par sujet

| Sujet | Document du dépôt |
|---|---|
| Index général | [`docs/README.md`](../README.md) |
| Architecture et flux | [`architecture-globale.md`](architecture-globale.md), [`flux-runtime.md`](flux-runtime.md) |
| Vocabulaire/certitude | [`guide-de-lecture-et-glossaire.md`](guide-de-lecture-et-glossaire.md) |
| Sauvegarde | [`sauvegarde-progression-et-integrite.md`](../01-systemes/sauvegarde-progression-et-integrite.md) |
| Run/vaisseaux/ressources | [`run-nodes-vaisseaux-et-ressources.md`](../01-systemes/run-nodes-vaisseaux-et-ressources.md) |
| Économie/modules | [`economie-boutique-et-modules.md`](../01-systemes/economie-boutique-et-modules.md) |
| Niveaux/spawn/physique | [`niveaux-spawn-balles-et-physique.md`](../01-systemes/niveaux-spawn-balles-et-physique.md) |
| Bacs/flush | [`paddle-bacs-et-flush.md`](../01-systemes/paddle-bacs-et-flush.md) |
| Score/objectifs/combos | [`score-objectifs-et-combos.md`](../01-systemes/score-objectifs-et-combos.md) |
| Fin/récompenses/reprise | [`fin-de-niveau-recompenses-et-reprise.md`](../01-systemes/fin-de-niveau-recompenses-et-reprise.md) |
| Input/tutoriel/pause | [`tutoriel-pause-input-et-plateformes.md`](../01-systemes/tutoriel-pause-input-et-plateformes.md) |
| UI, audio, FX, externe, debug | pages correspondantes de [`docs/01-systemes`](../01-systemes/) |
| Sources de vérité/valeurs | [`donnees-schemas-et-sources-de-verite.md`](../02-donnees-et-unity/donnees-schemas-et-sources-de-verite.md), [`equilibrage-et-configuration-active.md`](../02-donnees-et-unity/equilibrage-et-configuration-active.md) |
| Scènes/assets/réglages | pages correspondantes de [`docs/02-donnees-et-unity`](../02-donnees-et-unity/) |
| Classes/IDs/invariants | pages correspondantes de [`docs/03-reference`](../03-reference/) |
| Legacy/hybride | [`systemes-actifs-legacy-et-hybrides.md`](../04-etat-du-projet/systemes-actifs-legacy-et-hybrides.md) |
| Incertitudes/travaux | [`incertitudes-contradictions-et-travaux-en-cours.md`](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md) |

## 15. État Git de l’instantané

Au moment de la vérification, l’espace de travail n’était pas propre. Étaient déjà modifiés : quatre assets de polices TMP, `ComboDefinitionCatalog.asset`, `BallScore_TMP.prefab`, `DebugLauncher.unity`, `Main.unity`, `ShipRuntimeSetup.cs`, quatre scripts d’overlay combo et `ModuleCatalog.json`.

Étaient non suivis : deux nouveaux prefabs de score et leurs `.meta`, ainsi que `GameplayScoreImpactUI`, `ComboScoreUI`, `ScoreAttractorUI`, `ScoreFlushAbsorberUI` et leurs `.meta`. Le dossier `docs/` était également non suivi. Certaines références de la nouvelle chaîne de score étaient nulles dans le YAML de `Main` observé. Depuis cet instantané, le chemin V2 a été câblé et validé en Play Mode ; l’ancien chemin `ScoreBinder`/`ScoreUI`, le prefab `ScoreImpactPacketUI` et les deux scènes d’archive ont été retirés.

Cet état Git est un fait de l’instantané, pas une intention attribuée aux auteurs. Avant toute modification future, il faut relire l’état Git courant : cette liste peut être devenue obsolète.

## 16. Limites de cette synthèse

Ce document résume le projet sans inventorier les 258 scripts de jeu ni toutes les valeurs d’équilibrage. Il ne remplace pas les documents canoniques lorsqu’un dépôt est accessible. Les comportements runtime, mobiles, audiovisuels et de désérialisation non exécutés restent à valider. Pour raisonner système par système sans dépôt, poursuivre avec [AI_SYSTEMS_REFERENCE_404.md](AI_SYSTEMS_REFERENCE_404.md) ; pour préparer un travail, utiliser [AI_WORKING_GUIDE_404.md](AI_WORKING_GUIDE_404.md).
