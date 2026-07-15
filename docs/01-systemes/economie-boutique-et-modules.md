# Économie, boutique et modules

> **Périmètre** : argent de run, prix, offres, rerolls, réparation, inventaire et effets des modules.  
> **Statut** : confirmé par code et catalogues ; valeurs en cours de travail signalées séparément.  
> **Date de vérification** : 2026-07-15.  
> **Principaux appuis** : `EconomyConfig.cs`, `EconomyConfig_Default.asset`, `ModuleCatalog.json`, `ModuleCatalogService.cs`, services `RunModule*`, contrôleurs `RunHubModules*`, `ModuleRuntimeStats.cs`, `DroneRuntimeControllerBase.cs`, `K1AntiBlackDroneController.cs`, `K2DroneInterceptorController.cs`, `modules_shop_rules.json`.

## Monnaie et boutique

L’unique monnaie de gameplay retrouvée est l’argent du run. Elle est gagnée par certains modules/récompenses et dépensée pour acheter des modules, réparer la coque ou renouveler les offres. Elle disparaît avec la réinitialisation du run.

Les offres sont générées selon un jeu de règles de boutique, puis persistées avec le run afin qu’un rechargement ne les reroll pas implicitement. Des règles existent pour `W1_START` et `W1_MID`; une règle W2 est présente dans les données mais aucun monde W2 actif n’a été établi.

## Prix et tiers

Les modules ont trois tiers : bronze, argent et or. Les prix par tier et les coûts de réparation/reroll sont des données d’équilibrage ; voir la table canonique dans [Équilibrage et configuration active](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## Cycle d’un module

1. Le catalogue JSON définit identité, famille, tier, textes/clé de localisation, prix ou paramètres d’effet.
2. La boutique génère une offre et l’enregistre.
3. L’achat déplace l’identifiant vers l’inventaire du run et débite l’argent.
4. L’interface des systèmes équipe le module dans un slot compatible.
5. Les services runtime agrègent les modules équipés et exposent leurs statistiques/effets au gameplay.

Les slots proviennent de la définition du vaisseau. L’inventaire et l’équipement sont persistés dans la sauvegarde du run.

## Familles et effets observés

Le catalogue comporte 33 modules répartis sur onze familles :

| Famille | Effet fonctionnel observé |
|---|---|
| H | Réparation de coque ou gain d’argent lié à la coque. |
| SCAN / S | Informations de briefing/scanner. |
| GREED / G | Augmente le seuil de flush automatique. |
| C | Effets conditionnés à une coque pleine, dont maximum de coque ou delta de score. |
| A | Réserve une charge pour transformer provisoirement une noire dans un bac ; la charge est rendue si elle ressort, ou consommée au flush. La blanche obtenue peut ensuite être convertie par B. |
| B | Conversion de blanches vers bleues ou rouges. |
| E | Modification de durée de niveau. |
| F | Gain d’argent lié aux médailles de fin. |
| K0 | Contrôle transversal des drones : tous commencent chargés ; aux tiers 2 et 3, leurs cooldowns sont respectivement multipliés par `0,9` et `0,8`. K0 ne crée aucun drone et passe uniquement par le socle commun. |
| K1 | Drone anti-noire : recharge en 30/23/18 s selon le tier, priorise les noires dangereuses présentes dans les bacs puis la noire dangereuse la plus proche sur le plateau, et les neutralise sans score ni perte. |
| K2 | Drone Interceptor : recharge en 30/25/22 s ; sauve respectivement les billes blanches, blanches/bleues, puis blanches/bleues/rouges avant le Void et les téléporte vers le haut du plateau. |

L’ordre fonctionnel des transformations de bac est décrit dans [Paddle, bacs et flush](paddle-bacs-et-flush.md). Les bonus de fin sont décrits dans [Fin de niveau](fin-de-niveau-recompenses-et-reprise.md).

## K1 : drone anti-noire

`K1AntiBlackDroneController` vit dans le monde sous `WorldRoot/BoardRoot/DronesRoot`. Il effectue une ronde verticale à gauche du mur entre `Y = -3` et `Y = 3`, avec un X dérivé du bord extérieur du mur et d’une marge sérialisée. Il ne cible aucune noire au-dessus de `Y = 4`.

Sa présentation de charge réutilise le cooldown autoritaire du socle : le sprite cyan/halo se remplit radialement derrière le sprite déchargé, puis le sprite chargé remplace complètement le sprite déchargé à 100 %. La charge reste pleine tant qu’aucune cible admissible n’est disponible. Le tir est précédé d’un flash blanc, puis provoque un léger recul opposé au laser avant le retour sur le X de patrouille.

Après acquisition, K1 attend silencieusement `0,15 s`, revalide la cible puis tente une réservation terminale juste avant le premier affichage du laser. Une noire réservée et affichée blanche par la famille A n’est jamais admissible. Une bille déjà verrouillée par un snapshot (`collected`) ne l’est pas davantage.

Le départ visible du laser est le point de non-retour : K1 marque la bille comme possédée, la retire immédiatement de son bac logique si nécessaire, notifie le changement de contenu et consomme son cooldown. Le laser suit ensuite la position courante de la bille malgré les rebonds et la neutralisation est garantie. Un arrêt du gameplay pendant le trajet finalise immédiatement cette neutralisation. Le recyclage utilise `BallRecycleReason.Neutralized`, qui ne compte ni collecte, ni perte, ni dégât.

L’arbitrage avec les flushs est documenté dans [Paddle, bacs et flush](paddle-bacs-et-flush.md).

## K0 : contrôle transversal des drones

K0 est agrégé par `ModuleRuntimeStats` et consommé uniquement par `DroneRuntimeControllerBase`. Le socle demande à chaque drone son cooldown de base, lui applique `GetEffectiveDroneCooldown`, puis gère la progression, la charge stockée et les frontières de mission sans connaître la classe concrète du drone.

- T1 : tous les drones équipés commencent la mission chargés ; multiplicateur de cooldown `1,0`.
- T2 : même départ chargé ; multiplicateur `0,9`.
- T3 : même départ chargé ; multiplicateur `0,8`.

Cette architecture s’applique actuellement à K1 et K2 et reste ouverte aux futurs drones. K0 ne doit donc jamais contenir de référence directe vers `K1AntiBlackDroneController` ou `K2DroneInterceptorController`.

## K2 : drone Interceptor

`K2DroneInterceptorController` partage le socle de charge de K1 et possède sa propre présentation `uncharged/cooldown/charged`. Il patrouille horizontalement au-dessus du paddle et des bacs ; son amplitude X est la portée du paddle augmentée de `patrolExtraRange`.

Une `DroneInterceptionZone` événementielle est placée après la dernière possibilité normale de collecte et avant le Void. Elle ne signale que les billes descendantes, actives, hors bac, hors tutoriel et non déjà exclues du gameplay. K2 filtre ensuite leur couleur selon son tier : blanche en T1, blanche ou bleue en T2, blanche, bleue ou rouge en T3.

Lors de la saisie, K2 réserve atomiquement la bille via `BallState` : collider désactivé, vitesse annulée, Rigidbody rendu cinématique et exclusion temporaire des bacs, du Void, de K1 et des autres drones. La bille rejoint le centre du drone, un flash blanc masque sa disparition, puis le spawner choisit une position libre avec un X aléatoire dans la plage de spawn et un Y situé sous le plafond. Aucun nouveau projectile ni nouvelle impulsion n’est créé : la bille est relâchée à vitesse nulle et reprend sa physique normale.

À l’arrivée, le flash s’ouvre pendant que la bille est encore invisible. La bille réapparaît après `0,12 s`, tandis que le flash reste actif `0,42 s` au total. Les trails et effets de danger sont nettoyés pendant la transition pour éviter une trace entre les deux positions. Si la séquence est interrompue, la visibilité et l’état physique sont restaurés ; le nettoyage final force également la libération de toute exclusion de drone restante.

## État et limites

Le statut de travail du catalogue est centralisé dans [Incertitudes et travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md). Aucun système de monnaie permanente n’a été retrouvé ; l’emploi du terme « token » est défini dans le [glossaire](../00-vue-ensemble/guide-de-lecture-et-glossaire.md).
