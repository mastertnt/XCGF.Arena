# CLAUDE.md

Consignes pour Claude sur ce dépôt (XCGF.Arena, .NET 8, branche par défaut `master`).

## Une issue GitHub par tâche

Chaque nouvelle tâche demandée par l'utilisateur donne lieu à une issue GitHub sur `mastertnt/XCGF.Arena`, créée avant de commencer le travail.

- Titre court qui décrit la tâche, corps qui reprend la demande.
- Appliquer le label adéquat :
  - `enhancement` : nouvelle fonctionnalité ou évolution
  - `bug` : correction d'anomalie
  - `documentation` : README, CLAUDE.md, docs
  - `ci` : workflows GitHub Actions, build, intégration continue
  - `question` : demande d'analyse ou de réponse sans changement de code
- Si aucun label existant ne convient, créer le label manquant plutôt que d'en détourner un autre.
- La PR qui réalise la tâche référence l'issue (`Closes #<numéro>`).

## Build et tests

```bash
dotnet build
dotnet test
```
