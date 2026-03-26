set REGISTRY_URL=http://localhost:4873
call npm install tar || pause
node generate-registry.js || pause
