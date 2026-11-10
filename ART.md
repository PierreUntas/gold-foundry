# Gold Foundry — remplacer les machines par de vraies images

Le jeu dessine tout en code. Dès qu'un fichier **`Assets/GoldFoundry/Resources/Art/<nom>.png`** existe,
la machine correspondante est remplacée par l'image au lancement (les collisions et l'animation ne changent pas).
On peut les ajouter **une par une** : chaque image manquante garde son dessin en code.

La matière (minerai, or en fusion, lingots, pièces) reste toujours animée par le code : **ne la dessine pas dans les images.**

`art_layout_guide.png` (à la racine du projet) montre où chaque image se place, à l'échelle d'un écran 1080×2340.

## Les fichiers

| Fichier | Taille à l'écran (px) | Générer en | Ce qu'il doit montrer |
|---|---|---|---|
| `background.png` | 1080×2340 | 1080×2340 | La tour : mur de pierre sombre, deux piliers de pierre claire sur les bords, étagères de pierre aux hauteurs du gabarit. **Aucune machine, aucune matière.** |
| `pipe.png` | 151×302 | 300×600 | Tuyau vert vertical, bouche en bas. |
| `ore_conveyor.png` | 582×81 | 1160×160 | Tapis roulant à chaîne vu de face, sans minerai. |
| `piston.png` | 116×198 | 230×400 | Tête de piston bleu acier, face rayée jaune/noir tournée vers la gauche. |
| `furnace.png` | 455×373 | 910×750 | Four blindé bleu acier, voyants rouges, manomètre. **La fenêtre doit être transparente** (on y voit le bain et le minerai fondre). |
| `reservoir.png` | 299×266 | 600×530 | Cuve vitrée à cadre bleu acier, tuyau chromé coudé vers la droite puis le bas. **Cuve vide** (l'or est ajouté par le jeu, devant l'image). |
| `mold.png` | 117×56 | 240×110 | Un moule à lingot vide, vu de face. |
| `mold_conveyor.png` | 535×52 | 1070×105 | Tapis à chaîne bas et long, sans moules. |
| `press.png` | 128×58 | 260×120 | Bâti de la presse (partie fixe, fixée sous l'étagère). |
| `press_head.png` | 111×105 | 220×210 | Tête de presse qui descend : bloc bleu acier, collier rayé, matrice chromée en bas. |
| `bar_conveyor.png` | 518×58 | 1040×120 | Tapis à chaîne + enclume grise à droite, sans lingots. |
| `vault.png` | 354×177 | 710×350 | Coffre ouvert bleu acier, coins rayés jaune/noir, voyant vert. **Vide** (les pièces sont ajoutées par le jeu, devant). |

Toutes les images sauf `background.png` : **fond transparent (PNG)**, vue de face, sans perspective, sans texte.

## Prompt de base (à coller avant chaque demande)

> Stylized 3D mobile game asset in the style of the attached Gold Foundry illustrations: chunky cartoon
> industrial machines, navy steel panels with bevels and rivets, yellow-and-black hazard stripes, chrome
> details, soft warm lighting, clean readable silhouette. Straight front view (orthographic, no perspective),
> centered, isolated on a fully transparent background, no shadow on the ground, no text, no UI.

Puis ajoute la description de la ligne du tableau (en anglais, par exemple « an empty open coin vault… »)
et le format exact (« 710×350 px »). Joindre la couverture du document de vision comme référence de style aide beaucoup.

## Dans Unity

1. Crée le dossier `Assets/GoldFoundry/Resources/Art/` et glisse les PNG dedans (noms exacts, en minuscules).
2. Sélectionne-les → Inspector : **Alpha Is Transparency** coché, **Max Size** 2048 → Apply.
3. Play. Si une image est mal cadrée, ajuste-la dans l'image elle-même (marges transparentes),
   ou dis-moi lequel : la taille et la position se règlent en une ligne dans `GFFactory.cs` (appels `GFArt.Skin`).
