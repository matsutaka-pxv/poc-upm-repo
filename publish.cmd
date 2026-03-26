set REGISTRY_URL=https://matsutaka-pxv.github.io/poc-upm-repo
call npm install tar || pause
node generate-registry.js || pause
