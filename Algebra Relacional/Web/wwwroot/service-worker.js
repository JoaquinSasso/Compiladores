// In development, always fetch from the network and do not enable offline support.
// This is because caching would make development more difficult (changes would not
// be reflected on the first load after each change).
self.addEventListener('fetch', () => { });
// Cada compilación es una versión nueva: la página la ofrece con "Actualizar" (index.html).
self.addEventListener('message', event => { if (event.data === 'activar') self.skipWaiting(); });
