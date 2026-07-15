# Économie, boutique et modules

> **Périmètre** : argent de run, prix, offres, rerolls, réparation, inventaire et effets des modules.  
> **Statut** : confirmé par code et catalogues ; valeurs en cours de travail signalées séparément.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `EconomyConfig.cs`, `EconomyConfig_Default.asset`, `ModuleCatalog.json`, `ModuleCatalogService.cs`, services `RunModule*`, contrôleurs `RunHubModules*`, `modules_shop_rules.json`.

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

Le catalogue comporte 27 modules répartis sur neuf familles :

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
| K1 | Drone anti-noire : recharge en 30/23/18 s selon le tier, priorise les noires dangereuses présentes dans les bacs puis la noire dangereuse la plus proche sur le plateau, et les neutralise sans score ni perte. |

L’ordre fonctionnel des transformations de bac est décrit dans [Paddle, bacs et flush](paddle-bacs-et-flush.md). Les bonus de fin sont décrits dans [Fin de niveau](fin-de-niveau-recompenses-et-reprise.md).

## K1 : drone anti-noire

`K1AntiBlackDroneController` vit dans le monde sous `WorldRoot/BoardRoot/DronesRoot`. Il effectue une ronde elliptique dont le centre, les rayons et la vitesse dérivent progressivement ; sa limite basse active est `Y = -3`. Son cooldown radial reste visuellement plein tant qu’aucune cible admissible n’est disponible.

Après acquisition, K1 attend silencieusement `0,15 s`, revalide la cible puis tente une réservation terminale juste avant le premier affichage du laser. Une noire réservée et affichée blanche par la famille A n’est jamais admissible. Une bille déjà verrouillée par un snapshot (`collected`) ne l’est pas davantage.

Le départ visible du laser est le point de non-retour : K1 marque la bille comme possédée, la retire immédiatement de son bac logique si nécessaire, notifie le changement de contenu et consomme son cooldown. Le laser suit ensuite la position courante de la bille malgré les rebonds et la neutralisation est garantie. Un arrêt du gameplay pendant le trajet finalise immédiatement cette neutralisation. Le recyclage utilise `BallRecycleReason.Neutralized`, qui ne compte ni collecte, ni perte, ni dégât.

L’arbitrage avec les flushs est documenté dans [Paddle, bacs et flush](paddle-bacs-et-flush.md).

## État et limites

Le statut de travail du catalogue est centralisé dans [Incertitudes et travaux en cours](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md). Aucun système de monnaie permanente n’a été retrouvé ; l’emploi du terme « token » est défini dans le [glossaire](../00-vue-ensemble/guide-de-lecture-et-glossaire.md).
