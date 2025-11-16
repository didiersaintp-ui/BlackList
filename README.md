# EMV PAN Blacklist Management System

Système avancé de gestion de liste noire pour les PANs (Primary Account Numbers) EMV avec synchronisation delta en temps réel.

## 🎯 Objectifs du Projet

Ce projet démontre une architecture sophistiquée pour gérer efficacement des listes noires de 1+ million de PANs EMV avec:

- **Synchronisation Delta Ultra-Rapide**: Mise à jour toutes les 10 secondes
- **Trois Implémentations de Filtres Probabilistes**:
  - Cuckoo Filter
  - Quotient Filter
  - Hash Table + Golomb-Coded Sets
- **Zero Erreurs**: Validation rigoureuse et tests complets
- **Performance Optimale**: Benchmarks comparatifs détaillés

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      Docker Compose                          │
├─────────────────────────────────────────────────────────────┤
│                                                               │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   API        │  │   API        │  │   API        │      │
│  │   Cuckoo     │  │   Quotient   │  │   Golomb     │      │
│  │   :5001      │  │   :5002      │  │   :5003      │      │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘      │
│         │                  │                  │              │
│         │ Delta (1/sec)    │                  │              │
│         ▼                  ▼                  ▼              │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐      │
│  │   Client     │  │   Client     │  │   Client     │      │
│  │   Cuckoo     │  │   Quotient   │  │   Golomb     │      │
│  │   Sync 10s   │  │   Sync 10s   │  │   Sync 10s   │      │
│  └──────────────┘  └──────────────┘  └──────────────┘      │
│                                                               │
└─────────────────────────────────────────────────────────────┘
```

## 🚀 Démarrage Rapide

### Prérequis

- Docker Desktop pour Windows
- 8 GB RAM minimum recommandé
- 10 GB espace disque disponible

### Lancement du Projet

```bash
# Cloner le repository
git clone <repository-url>
cd BlackList

# Construire et lancer tous les services
docker-compose up --build

# Ou lancer un filtre spécifique
docker-compose up api-cuckoo client-cuckoo
```

### Accès aux Services

- **Swagger UI (Cuckoo)**: http://localhost:5001/swagger
- **Swagger UI (Quotient)**: http://localhost:5002/swagger
- **Swagger UI (Golomb)**: http://localhost:5003/swagger

## 📊 Comparaison des Filtres

### Cuckoo Filter
- ✅ **Avantages**:
  - Très faible taux de faux positifs (< 0.01%)
  - Support natif des suppressions
  - Excellentes performances en lecture
- ⚠️ **Inconvénients**:
  - Peut échouer lors d'insertions avec table pleine
  - Taille fixe après initialisation

### Quotient Filter
- ✅ **Avantages**:
  - Cache-friendly (meilleure localité)
  - Support des suppressions
  - Bon taux de compression
- ⚠️ **Inconvénients**:
  - Plus complexe à implémenter
  - Performances variables selon le taux de remplissage

### Hash Table + Golomb
- ✅ **Avantages**:
  - **ZERO faux positifs** (hash table exacte)
  - Excellente compression avec Golomb coding
  - Idéal pour transmission réseau
- ⚠️ **Inconvénients**:
  - Plus lent pour les opérations individuelles
  - Nécessite re-compression après modifications

## 🔧 API Endpoints

### GET /api/blacklist/initial
Récupère le filtre initial complet

**Query Parameters:**
- `filterType` (optional): CuckooFilter | QuotientFilter | HashTableGolomb

**Response:**
```json
{
  "filterData": "base64-encoded-filter",
  "timestamp": 1234567890,
  "totalCount": 1000000,
  "filterType": "CuckooFilter",
  "metadata": {
    "capacity": 1200000,
    "bucketSize": 4,
    "fingerprintSize": 2,
    "falsePositiveRate": 0.0001
  }
}
```

### POST /api/blacklist/deltas
Récupère les deltas depuis un timestamp

**Request:**
```json
{
  "lastTimestamp": 1234567890,
  "count": 10
}
```

**Response:**
```json
{
  "deltas": [
    {
      "timestamp": 1234567891,
      "addedPans": ["1234567890123456"],
      "removedPans": ["9876543210987654"]
    }
  ],
  "currentTimestamp": 1234567900
}
```

### POST /api/blacklist/validate
Valide l'exactitude du filtre

**Request:**
```json
{
  "testPans": ["1234567890123456", "9876543210987654"],
  "expectedResults": [true, false]
}
```

**Response:**
```json
{
  "totalTests": 2,
  "correctMatches": 2,
  "falsePositives": 0,
  "falseNegatives": 0,
  "accuracy": 1.0,
  "falsePositiveRate": 0.0
}
```

### GET /api/blacklist/status
Obtient le statut actuel du filtre

**Response:**
```json
{
  "filterType": "CuckooFilter",
  "count": 1000000,
  "metadata": { ... }
}
```

## 🧪 Tests et Benchmarks

### Exécuter les Tests Unitaires

```bash
# Dans le conteneur ou localement avec .NET 8
dotnet test tests/EMVBlacklist.Tests/EMVBlacklist.Tests.csproj
```

### Exécuter les Benchmarks

```bash
# Localement avec .NET 8
cd src/EMVBlacklist.Benchmarks
dotnet run -c Release
```

### Résultats Attendus

Les benchmarks comparent:
1. **Temps d'insertion**: Temps pour insérer 1M éléments
2. **Taille de données**: Taille du filtre sérialisé
3. **Temps de recherche**: Temps pour 10K recherches
4. **Taux de faux positifs**: Mesuré sur 10K tests négatifs
5. **Performance delta**: Temps pour appliquer 100 ajouts/suppressions

## 📁 Structure du Projet

```
BlackList/
├── src/
│   ├── EMVBlacklist.API/          # Backend API .NET 8
│   │   ├── Controllers/           # Contrôleurs REST
│   │   ├── Services/              # Services métier
│   │   └── Dockerfile
│   ├── EMVBlacklist.Client/       # Client de synchronisation
│   │   └── Dockerfile
│   ├── EMVBlacklist.Shared/       # Code partagé
│   │   ├── Filters/               # Implémentations des filtres
│   │   ├── Models/                # Modèles de données
│   │   └── Interfaces/            # Interfaces
│   └── EMVBlacklist.Benchmarks/   # Tests de performance
├── tests/
│   └── EMVBlacklist.Tests/        # Tests unitaires
├── docker-compose.yml             # Orchestration Docker
└── README.md
```

## 🎓 Concepts Techniques

### Cuckoo Filter

Le Cuckoo Filter utilise le "cuckoo hashing" avec deux fonctions de hash. Chaque élément peut être placé dans deux positions possibles, offrant une excellente distribution et permettant les suppressions.

**Complexité:**
- Insertion: O(1) amortized
- Recherche: O(1)
- Suppression: O(1)

### Quotient Filter

Stocke le quotient et le reste du hash. Utilise des bits de métadonnées (occupied, continuation, shifted) pour gérer les collisions de manière compacte.

**Complexité:**
- Insertion: O(1) amortized
- Recherche: O(1) average
- Suppression: O(1) average

### Hash Table + Golomb

Combine un hash table exact avec compression Golomb pour sérialisation efficace. Golomb code les deltas entre hashes triés.

**Complexité:**
- Insertion: O(1)
- Recherche: O(1)
- Sérialisation: O(n log n)

## 🔒 Sécurité EMV

Ce projet gère des PANs fictifs pour démonstration. En production:

- Utiliser le PAN truncated (6 premiers + 4 derniers chiffres)
- Hasher les PANs avec clé secrète
- Implémenter le chiffrement TLS pour les API
- Logs sans données sensibles
- Conformité PCI DSS

## 📈 Monitoring

Les logs montrent en temps réel:
- Nombre d'éléments dans le filtre
- Deltas appliqués (ajouts/suppressions)
- Performance des opérations
- Taux de synchronisation

## 🤝 Contribution

Structure pour extensions futures:
- Bloom Filters
- XOR Filters
- Count-Min Sketch pour statistiques
- Redis pour persistance
- Prometheus métriques

## 📝 License

MIT License - Voir LICENSE file

## 👥 Auteur

Projet de démonstration pour gestion de listes noires EMV PAN.

---

## 🆘 Troubleshooting

### L'API ne démarre pas
```bash
# Vérifier les logs
docker-compose logs api-cuckoo

# Redémarrer le service
docker-compose restart api-cuckoo
```

### Le client ne se connecte pas
```bash
# Vérifier que l'API est prête
curl http://localhost:5001/api/blacklist/status

# Vérifier les logs du client
docker-compose logs client-cuckoo
```

### Performances lentes
- Augmenter la RAM allouée à Docker Desktop (8GB minimum)
- Réduire INITIAL_COUNT dans docker-compose.yml
- Utiliser un SSD pour Docker volumes

## 📞 Support

Pour questions et support:
- Créer une issue sur GitHub
- Consulter la documentation dans `/docs`
- Examiner les logs: `docker-compose logs -f`
