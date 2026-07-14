# Maintenance documentaire

> **Périmètre** : règles de mise à jour de `docs/` afin de conserver une documentation cohérente et sans duplication.  
> **Statut** : procédure documentaire canonique.  
> **Date de vérification** : 2026-07-14.  
> **Principaux appuis** : structure de `docs/`, conventions du [guide de lecture](../00-vue-ensemble/guide-de-lecture-et-glossaire.md).

## Principe

Une information détaillée possède une seule page canonique. Les autres pages résument la relation et pointent vers cette page ou son titre de section. Les fichiers source Unity/JSON restent toujours l’autorité finale sur le logiciel ; la documentation indique la date à laquelle elle les a observés.

## Propriété des sujets

| Type d’information | Page canonique |
|---|---|
| Parcours et responsabilités globales | `00-vue-ensemble` |
| Comportement d’un système | page correspondante de `01-systemes` |
| Propriétaire/schéma d’une donnée | `donnees-schemas-et-sources-de-verite.md` |
| Valeur d’équilibrage structurante | `equilibrage-et-configuration-active.md` |
| Câblage d’une scène/asset | page correspondante de `02-donnees-et-unity` |
| Classe, identifiant, chemin ou invariant | `03-reference` |
| Incertitude ou contradiction | `incertitudes-contradictions-et-travaux-en-cours.md` |
| Statut legacy/hybride | `systemes-actifs-legacy-et-hybrides.md` |

## Métadonnées obligatoires

Chaque document conserve en tête : **Périmètre**, **Statut**, **Date de vérification** et **Principaux appuis**. Une mise à jour partielle doit préciser si certaines sections n’ont pas été revérifiées. Le statut ne devient pas « confirmé » sur la base d’un nom de fichier seul.

## Mise à jour après changement du projet

1. Identifier le système propriétaire du changement.
2. Relire ses sources, données et câblages concernés.
3. Mettre à jour d’abord la page canonique et sa date/statut.
4. Mettre à jour les index uniquement si un point d’entrée, identifiant ou lien change.
5. Déplacer toute nouvelle ambiguïté dans le registre d’incertitudes.
6. Vérifier les liens Markdown et l’absence de copie divergente d’une valeur.

## Création ou découpage d’une page

Une nouvelle page principale est justifiée lorsqu’un système possède sa propre source de vérité, son cycle de vie et plusieurs consommateurs, ou lorsqu’une page existante devient difficile à parcourir malgré une table des matières interne. Un détail local, une classe isolée ou une variante de présentation reste dans la page de son système.

L’objectif reste environ 20 à 30 documents principaux. Si un découpage devient nécessaire, `README.md` et tous les renvois doivent être ajustés dans le même changement documentaire.

## Certitude et historique

Toute conclusion nécessitant Play Mode doit rester **incertaine** tant qu’elle n’a pas été observée et datée. Lorsqu’une incertitude est résolue, son résultat rejoint la page système canonique et l’entrée du registre est retirée ou reformulée comme limite historique seulement si cette histoire reste utile à la compréhension du code présent.
