# Journal des modifications

Ce fichier est la traduction française de CHANGELOG.md, qui reste l’original. Il présente les nouveautés de chaque version de Glide pour Windows. Glide affiche les nouvelles entrées une seule fois après une mise à jour, et les notes de chaque version sur GitHub en sont issues.

## 0.9.2 — 2026-10-16

### Nouveautés

- **Les Nouveautés parlent aussi votre langue.** Les notes que Glide affiche après une mise à jour, et dans Nouveautés, sont désormais dans la même langue que le reste de Glide.

## 0.9.1 — 2026-10-16

### Nouveautés

- **Glide parle votre langue.** Glide existe maintenant en français, allemand, japonais, chinois simplifié, coréen et portugais brésilien, en plus de l’anglais et de l’espagnol, programme d’installation compris. Il suit la langue d’affichage de Windows, ou choisissez-en une dans Paramètres › Général › Langue.

## 0.9.0 — 2026-10-15

### Nouveautés

- **Voyez où atterrira une fenêtre que vous déplacez.** Pendant que vous faites glisser une fenêtre, un cadre de la couleur du contour de focus indique la colonne qu’elle rejoindrait, barre d’onglets comprise. Au-dessus d’un autre écran, une barre montre où elle se placerait entre les colonnes de cet écran. Sans surbrillance, elle reprendra sa place.
- **Un menu de la zone de notification plus complet.** Il liste les bureaux utilisés avec le nombre de fenêtres de chacun ; choisissez-en un pour y aller. Sous **Raccourcis** figure chaque action avec son raccourci, pour l’exécuter depuis le menu. Quand « Remplacer les raccourcis Windows » est désactivé, le menu liste aussi les raccourcis que Windows conserve.
- **Tous les raccourcis sur une seule page.** Win+Ctrl+/ (la touche qui porte le ? sur un clavier américain) les affiche tous ; appuyez dessus de nouveau ou sur Échap pour fermer. Vous les trouvez aussi dans le menu de la zone de notification, sous Raccourcis.
- **Signaler un problème**, dans le menu de la zone de notification et dans Paramètres › Général. Glide place son journal et ses paramètres dans un fichier zip, sans les titres des fenêtres, et ouvre un nouveau ticket sur GitHub pour que vous décriviez ce qui s’est passé et joigniez le zip. Rien n’est envoyé automatiquement.
- **Glide habla español.** Quand Windows est en espagnol, Glide aussi, programme d’installation compris. Les Nouveautés restent en anglais.

### Corrections

- **Le plein écran reste en plein écran.** Une vidéo, un jeu ou F11 dans un navigateur n’est plus ramené dans sa colonne. Glide n’y touche pas, masque par-dessus le contour de focus, l’atténuation et la barre d’onglets, et laisse la molette horizontale et les balayages du pavé tactile l’atteindre.
- Passer à un autre bureau virtuel de Windows puis revenir conserve l’ordre de vos colonnes.
- Fermer une fenêtre ne retire plus le focus aux fenêtres de Glide, comme Nouveautés ou Paramètres.

## 0.8.1 — 2026-10-14

### Corrections

- Cliquer sur **Continuer** dans la fenêtre Nouveautés fermait Glide. Fermer une fenêtre laisse maintenant Glide en cours d’exécution.
- L’icône de la zone de notification montre tous les bureaux utilisés : une grille 3×3 des neuf bureaux, ceux utilisés allumés et le bureau actif le plus lumineux. Avec un seul bureau utilisé, elle affiche son numéro comme avant.

## 0.8.0 — 2026-10-14

### Nouveautés

- **Atténuer les fenêtres inactives.** Les fenêtres situées derrière la fenêtre active sur son écran s’assombrissent, pour que la fenêtre active ressorte. Désactivé par défaut ; activez cette option, et réglez l’intensité, dans Paramètres › Apparence.
- **Tous les bureaux utilisés sont listés.** L’info-bulle et le menu de l’icône de la zone de notification montrent tous les bureaux qui contiennent des fenêtres. L’icône affiche toujours le bureau actif.
- **Les numéros de bureau affichés à l’écran lors du changement** peuvent être désactivés dans Paramètres › Apparence.
- **Installer les mises à jour automatiquement.** Désactivé par défaut. Activez cette option dans Paramètres › Mises à jour, et Glide installe une nouvelle version dès qu’il la trouve, puis redémarre.

### Changements

- Les versions ne sont pas encore signées numériquement, donc Windows SmartScreen peut afficher un avertissement la première fois que vous lancez Glide : choisissez **Informations complémentaires › Exécuter quand même**.
- La déclaration de confidentialité du programme d’installation ne renvoie plus vers une politique de signature de code.

## 0.7.0 — 2026-10-14

### Nouveautés

- **Glide se met à jour tout seul.** Deux fois par jour, il recherche une nouvelle version et vous la propose depuis l’icône de la zone de notification. Chaque téléchargement est vérifié avant l’installation, et Glide remet vos fenêtres en place, se met à jour et redémarre. Désactivez cette option dans Paramètres › Mises à jour, ou pendant l’installation.
- **Nouveautés.** Après une mise à jour, Glide affiche ce qui a changé, comme ici. Vous les retrouvez dans le menu de la zone de notification.
- **Les apps retrouvent leur place.** Glide mémorise l’écran, le bureau, la largeur de colonne et la place sur la bande de chaque app. La fenêtre d’une app rouverte s’ouvre là, et après un redémarrage vos fenêtres ouvertes sont disposées de la même façon. Désactivez cette option ou oubliez tout dans Paramètres › Général.
- **Glide est open source**, sous licence MIT : [github.com/QuackByte/glide-windows](https://github.com/QuackByte/glide-windows).

### Changements

- Le programme d’installation affiche la déclaration de confidentialité de Glide et vous laisse choisir si Glide recherche les mises à jour.

## 0.6.0 — 2026-10-14

### Nouveautés

- **Des icônes d’apps** à côté des titres de fenêtres dans la vue d’ensemble et dans les barres d’onglets.
- **La vue d’ensemble défile** quand une bande est trop longue pour l’écran, au lieu de réduire les vignettes jusqu’à les rendre illisibles. La molette fait défiler la ligne située sous le pointeur.

### Corrections

- Les barres d’onglets glissent avec leur colonne au lieu de la devancer d’un bond.
- Après avoir empilé ou désempilé une fenêtre, le contour de focus pouvait apparaître à l’ancien emplacement de la fenêtre. Il la suit maintenant.

## 0.5.0 — 2026-10-14

### Nouveautés

- **Plusieurs écrans** : chaque écran a sa propre bande et ses neuf bureaux. Win+Alt+←/→ déplacent le focus d’un écran à l’autre ; Win+Alt+Shift+←/→ y envoient la colonne active.
- **Glisser-déposer** : déposez une fenêtre sur une barre d’onglets pour l’ajouter aux onglets, sur une autre fenêtre pour l’y empiler, ou sur un autre écran pour la déplacer.
- **Un contour de focus pour toutes les apps**, y compris les apps Electron, avec une épaisseur réglable.
- **Les balayages à trois doigts sur le pavé tactile** font glisser la bande.
- « Lancer Glide à l’ouverture de session » dans le menu de la zone de notification.

## 0.4.1 — 2026-10-14

La première version de Glide pour Windows.
