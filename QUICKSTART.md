# Guide de Démarrage Rapide

## 🚀 Lancement en 3 minutes

### Option 1: Script Automatique (Recommandé)

**Windows:**
```batch
start.bat
```

**Linux/Mac:**
```bash
./start.sh
```

Choisissez ensuite le filtre à tester (1-4).

### Option 2: Docker Compose Manuel

**Un seul filtre (moins de RAM):**
```bash
# Cuckoo Filter
docker-compose up --build api-cuckoo client-cuckoo

# Quotient Filter
docker-compose up --build api-quotient client-quotient

# Hash Table + Golomb
docker-compose up --build api-golomb client-golomb
```

**Tous les filtres en parallèle (8GB+ RAM requis):**
```bash
docker-compose up --build
```

## 📊 Que va-t-il se passer?

### Phase 1: Démarrage (30-60 secondes)

```
Building images...
Starting containers...
```

### Phase 2: Initialisation (1-2 minutes)

Chaque API va:
1. Créer son filtre spécifique
2. Insérer 1,000,000 PANs
3. Afficher les statistiques

**Exemple de sortie:**
```
emv-api-cuckoo    | Initializing blacklist with 1,000,000 PANs using CuckooFilter...
emv-api-cuckoo    | Added 100,000 PANs...
emv-api-cuckoo    | Added 200,000 PANs...
...
emv-api-cuckoo    | Initialization complete in 2.34 seconds
emv-api-cuckoo    | Filter size: 1,000,000 items
```

### Phase 3: Synchronisation Client (immédiat)

Le client va:
1. Se connecter à l'API
2. Télécharger le filtre complet
3. Commencer la synchronisation delta

**Exemple de sortie:**
```
emv-client-cuckoo | Waiting for API to be ready...
emv-client-cuckoo | API is ready!
emv-client-cuckoo |
emv-client-cuckoo | Initial load received:
emv-client-cuckoo |   Filter Type: CuckooFilter
emv-client-cuckoo |   Total Count: 1,000,000
emv-client-cuckoo |   Filter Size: 2,400,000 bytes
emv-client-cuckoo |
emv-client-cuckoo | Filter deserialized successfully
emv-client-cuckoo | Starting delta synchronization (every 10 seconds)...
```

### Phase 4: Synchronisation Continue

Toutes les 10 secondes, vous verrez:

```
[14:32:15] Received 10 deltas
  Total deltas processed: 10
  Total added: 6, Total removed: 4
  Current filter count: 1,000,002

[14:32:25] Received 10 deltas
  Total deltas processed: 20
  Total added: 13, Total removed: 7
  Current filter count: 1,000,006
```

## 🔍 Tester l'API

### Via Browser (Swagger UI)

Ouvrez dans votre navigateur:
- **Cuckoo Filter**: http://localhost:5001/swagger
- **Quotient Filter**: http://localhost:5002/swagger
- **Hash Table + Golomb**: http://localhost:5003/swagger

### Via cURL

**Obtenir le statut:**
```bash
curl http://localhost:5001/api/blacklist/status
```

**Récupérer le filtre initial:**
```bash
curl http://localhost:5001/api/blacklist/initial
```

**Obtenir les deltas:**
```bash
curl -X POST http://localhost:5001/api/blacklist/deltas \
  -H "Content-Type: application/json" \
  -d '{"lastTimestamp": 0, "count": 10}'
```

**Valider le filtre:**
```bash
curl -X POST http://localhost:5001/api/blacklist/validate \
  -H "Content-Type: application/json" \
  -d '{
    "testPans": ["1234567890123456"],
    "expectedResults": [true]
  }'
```

## 📈 Comparer les Performances

### Pendant l'exécution

Observez les logs pour comparer:

1. **Temps d'initialisation**: Combien de temps pour charger 1M PANs?
2. **Taille du filtre**: Combien d'octets pour la sérialisation?
3. **Performance delta**: Temps pour appliquer les updates

### Benchmarks Détaillés

**Exécuter les benchmarks localement:**

```bash
# Windows
run-benchmarks.bat

# Linux/Mac
./run-benchmarks.sh
```

Résultats sauvegardés dans `BenchmarkDotNet.Artifacts/`

## 🎯 Résultats Attendus

### Cuckoo Filter
- ✅ **Rapide**: ~2-3 secondes pour 1M insertions
- ✅ **Compact**: ~2.3 MB sérialisé
- ✅ **Équilibré**: Bon compromis performance/taille
- ⚠️ **Faux Positifs**: ~0.01-0.02%

### Quotient Filter
- ✅ **Précis**: Très faible taux FP (~0.001%)
- ✅ **Cache-friendly**: Bonne localité
- ⚠️ **Plus grand**: ~5 MB sérialisé
- ⚠️ **Plus lent**: ~3-4 secondes pour 1M insertions

### Hash Table + Golomb
- ✅ **Zero FP**: Aucun faux positif (hash exacte)
- ✅ **Ultra-compact**: ~1.2 MB sérialisé
- ✅ **Très rapide**: ~1-2 secondes pour 1M insertions
- ⚠️ **Moins flexible**: Re-compression nécessaire

## 🛑 Arrêter les Services

```bash
# Arrêt propre
Ctrl + C

# Puis nettoyer
docker-compose down

# Nettoyer complètement (images + volumes)
docker-compose down --rmi all --volumes
```

## 🔧 Personnalisation

### Modifier le nombre initial de PANs

Éditez `docker-compose.yml`:

```yaml
environment:
  - INITIAL_COUNT=500000  # Au lieu de 1000000
```

### Modifier la fréquence de delta

Dans `src/EMVBlacklist.API/Services/BlacklistService.cs`:

```csharp
// Ligne ~20
_deltaGeneratorTimer = new Timer(GenerateDelta, null,
    TimeSpan.FromSeconds(0.5),  // Au lieu de 1 seconde
    TimeSpan.FromSeconds(0.5));
```

Dans `src/EMVBlacklist.Client/BlacklistClient.cs`:

```csharp
// Ligne ~65
_syncTimer.Change(TimeSpan.Zero,
    TimeSpan.FromSeconds(5));  // Au lieu de 10 secondes
```

## ❓ Problèmes Courants

### "Docker is not running"

**Solution**: Démarrez Docker Desktop

### "Port already in use"

**Solution**:
```bash
# Trouver le processus
netstat -ano | findstr :5001  # Windows
lsof -i :5001                  # Linux/Mac

# Arrêter le processus ou changer le port dans docker-compose.yml
```

### "Out of memory"

**Solution**:
1. Augmenter RAM allouée à Docker (Paramètres → Resources)
2. Réduire INITIAL_COUNT dans docker-compose.yml
3. Lancer un seul filtre à la fois

### Le client ne trouve pas l'API

**Solution**:
```bash
# Vérifier que l'API est démarrée
docker-compose logs api-cuckoo

# Attendre que le healthcheck passe
docker-compose ps
```

## 📚 Documentation Complète

- **README.md**: Vue d'ensemble et guide complet
- **TECHNICAL.md**: Détails techniques et implémentation
- **Code source**: Commentaires inline

## 🎓 Prochaines Étapes

1. ✅ Exécuter le projet
2. ✅ Observer les logs
3. ✅ Tester l'API via Swagger
4. ✅ Comparer les trois filtres
5. ✅ Exécuter les benchmarks
6. 📖 Lire la documentation technique
7. 🔧 Expérimenter avec les paramètres
8. 🚀 Adapter pour votre use case

## 💡 Tips

- Laissez tourner au moins 2-3 minutes pour voir les deltas s'accumuler
- Utilisez `docker-compose logs -f client-cuckoo` pour suivre un client spécifique
- Comparez les tailles de filtres dans les logs initiaux
- Testez la validation via Swagger UI

Bon test! 🚀
