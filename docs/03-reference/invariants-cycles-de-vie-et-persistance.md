# Invariants, cycles de vie et persistance

> **Périmètre** : règles structurelles à préserver mentalement pour comprendre le runtime actuel.  
> **Statut** : invariants déduits de garde-fous explicites ; exceptions incertaines référencées.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : services Boot, `RunSessionState`, `SaveManager`, contrôleurs de niveau/fin, systèmes de bac et modules.

## Cycle global

- Le root Boot doit exister une seule fois et survivre aux changements de scène.
- Un parcours normal possède un profil chargé avant la création/continuation du run.
- `Main` doit disposer d’un `LevelContext`, issu du nœud courant ou d’une injection debug/standalone.
- Les catalogues statiques sont résolus par identifiant ; le run persiste ces identifiants, pas des instances Unity.

## Run

- La coque courante est bornée par le maximum calculé du vaisseau et de ses bonus.
- Les vies de contrat sont distinctes de la coque.
- Argent, modules et score de run sont transitoires ; meilleur score et tutoriel appartiennent au profil permanent.
- Une offre générée est persistée jusqu’à achat/reroll/changement de boutique prévu.
- Un module équipé doit correspondre à un module possédé et à un slot du run selon les services d’équipement.

## Niveau et bac

- Une balle ne doit être résolue qu’une fois par collecte/flush avant recyclage.
- Le snapshot de bac est la frontière entre contenu physique et calcul de flush.
- Les transformations de modules précèdent la conséquence finale des noires.
- Une noire ne compte pas pour la progression principale.
- Le nettoyage final précède l’évaluation complète et le snapshot de fin.

## Transaction de fin

- Une fin préparée possède un token et un snapshot persistés avant sa consommation.
- Le résultat présenté provient du snapshot, afin d’être rejouable après interruption.
- Un même token ne doit être commité qu’une fois.
- Après commit, l’état de fin en attente est effacé et le run pointe vers son nouvel état.

## Durées de vie statiques

`ChainRuntimeState` et `TimingRuntimeState` sont statiques. Leur reset régulier de mission n’a pas été retrouvé ; ils constituent donc une exception potentielle à l’isolation attendue d’un niveau. Ce point reste **incertain** et n’est pas transformé ici en invariant.

## Frontières de confiance

Les invariants sont imposés par le client local et ses gardes. Les chemins debug, l’édition de PlayerPrefs ou une modification du client peuvent les contourner. La page [Sauvegarde, progression et intégrité](../01-systemes/sauvegarde-progression-et-integrite.md) décrit précisément cette limite.
