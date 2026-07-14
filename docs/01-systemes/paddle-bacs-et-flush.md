# Paddle, bacs et flush

> **Périmètre** : contrôle du paddle, collecte dans les bacs, fermeture, seuil automatique et pipeline de résolution d’un flush.  
> **Statut** : confirmé par analyse statique ; sensation et ordre de callbacks non testés.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : `PlayerController.cs`, `BinCollector.cs`, `BinTrigger.cs`, `CloseBinController.cs`, `BlackFilterRuntimeController.cs`, `FlushResolutionEngine.cs`, services runtime de modules.

## Paddle

`PlayerController` déplace le paddle dans les limites configurées et calcule un rebond personnalisé en fonction du point d’impact. Les voies de commande et leur statut exact sont décrits dans [Tutoriel, pause, input et plateformes](tutoriel-pause-input-et-plateformes.md), avec les ambiguïtés centralisées dans le registre d’incertitudes.

## Collecte

Chaque bac accumule les balles entrées par son trigger. `BinCollector` tient les comptes et peut produire un `BinSnapshot`. Les contrôleurs visuels et icônes suivent l’état ouvert/fermé et la contamination par les noires.

Un flush peut être demandé par le joueur/contrôle de fermeture, par le seuil automatique ou par la séquence de fin. Le seuil de base est cinq balles ; les modules GREED l’augmentent. Le flush final fait partie du cycle d’évacuation.

## Pipeline canonique d’un flush

1. Le bac fige son contenu dans un snapshot.
2. Les effets de modules transforment les types admissibles : filtres A sur les noires, puis conversions B sur les blanches selon les services retrouvés.
3. La résolution calcule valeurs de balles, règles de combos runtime et modificateurs applicables.
4. Les balles noires restantes appliquent leur conséquence de coque.
5. Le `ScoreManager` reçoit le résultat et met à jour score, progression, pertes, historique et compteurs.
6. Les événements alimentent overlays, audio et feedbacks.
7. Les objets balle sont recyclés et le bac revient à l’état vide.

L’évaluation détaillée du score et des combos est canonique dans [Score, objectifs et combos](score-objectifs-et-combos.md). Les effets des familles de modules sont inventoriés dans [Économie, boutique et modules](economie-boutique-et-modules.md).

## Balles noires

Une noire n’ajoute pas de progression. Si elle n’a pas été transformée/neutralisée par un effet équipé, elle contribue à la perte et au dommage de coque. `BlackFilterRuntimeController` gère les charges de conversion associées. Le feedback de menace est distinct de cette logique fonctionnelle.

## Incertitude de score

Une ambiguïté existe sur l’intégration du score de base dans le parcours actif. Le constat technique n’est décrit qu’une fois dans [Incertitudes et contradictions](../04-etat-du-projet/incertitudes-contradictions-et-travaux-en-cours.md).
