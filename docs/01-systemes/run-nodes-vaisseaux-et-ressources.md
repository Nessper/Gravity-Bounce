# Run, nœuds, vaisseaux et ressources

> **Périmètre** : création et progression d’un run, plan de nœuds, sélection de vaisseau, coque et vies de contrat.  
> **Statut** : confirmé, avec ambiguïtés de fin de plan et de déblocage signalées.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `NewRunInitializer.cs`, `RunSessionState.cs`, `RunPlan.cs`, `RunHubController.cs`, `WorldCatalog.json`, `ShipCatalog.json`, `HullSystem.cs`.

## Initialisation

Un nouveau run reçoit un nouvel identifiant, le vaisseau sélectionné, son maximum de coque, les vies de contrat initiales, un score remis à zéro, l’argent initial défini par le vaisseau, un inventaire/équipement réinitialisé et un plan construit depuis le monde choisi. Les valeurs configurables sont référencées dans [Équilibrage](../02-donnees-et-unity/equilibrage-et-configuration-active.md).

## Plan W1 observé

Le `WorldCatalog` décrit une grammaire de tokens transformée en nœuds. Pour W1, le parcours est : boutique de départ, W1-L1, W1-L2, W1-L3, boutique intermédiaire, W1-L4, W1-L5, boss W1-L6, fin. Les nœuds boutique sont traités dans `RunHub`; les nœuds de mission chargent `Main`.

Le nœud courant et le plan persistent avec le run. La frontière d’index représentant la fin présente une divergence statique, détaillée uniquement dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Vaisseaux

Le catalogue contient :

- `CORE_SCOUT`, vaisseau normal débloqué par défaut ;
- `AETHER_RUNNER`, vaisseau normal non débloqué par défaut dans le catalogue ;
- `DEBUG_SHIP`, marqué caché dans les données.

La sélection renseigne l’identifiant utilisé ensuite pour la coque, les slots et les visuels. Les divergences de slots ainsi que l’application de `hidden` et des déblocages sont centralisées dans la page des [incertitudes](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).

## Deux ressources de survie

La **coque** appartient au vaisseau pendant le run. Les balles noires peuvent l’endommager lors de la résolution d’un bac ; des modules et la boutique peuvent la restaurer ou augmenter son maximum. L’épuisement est surveillé par `HullGameOverWatcher`.

Les **vies de contrat** appartiennent au parcours. Une mission perdue peut en consommer une, ce qui détermine si le run peut continuer. Elles sont présentées séparément dans le HUD et le hub. Ces ressources ne doivent pas être confondues.

## Hub

`RunHubController` choisit la vue correspondant au nœud. Le hub présente le statut du vaisseau, les étoiles du monde, la boutique et les systèmes d’équipement. Les détails économiques et de modules résident dans [Économie, boutique et modules](economie-boutique-et-modules.md).
