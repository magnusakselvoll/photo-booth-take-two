export const translations = {
  en: {
    // Download page
    enterPhotoCode: 'Photo code',
    findPhoto: 'Find Photo',
    getPhoto: 'Get Photo',
    downloadPhoto: 'Download Photo',
    sharePhoto: 'Share Photo',

    // Photo grid
    loadingPhotos: 'Loading photos...',
    failedToLoadPhotos: 'Failed to load photos',
    noPhotosYet: 'No photos yet',

    // Photo detail page
    loading: 'Loading...',
    photoNotFoundError: 'Photo not found',
    backToGallery: 'Back to Gallery',
    previousPhoto: 'Previous photo',
    nextPhoto: 'Next photo',

    // Slideshow
    noPhotosToShow: 'No photos to show yet',

    // Not found page
    pageNotFound: 'Page not found',
    goToGallery: 'Go to Gallery',
  },
  es: {
    // Download page
    enterPhotoCode: 'Código de foto',
    findPhoto: 'Buscar Foto',
    getPhoto: 'Obtener Foto',
    downloadPhoto: 'Descargar Foto',
    sharePhoto: 'Compartir Foto',

    // Photo grid
    loadingPhotos: 'Cargando fotos...',
    failedToLoadPhotos: 'Error al cargar las fotos',
    noPhotosYet: 'Aún no hay fotos',

    // Photo detail page
    loading: 'Cargando...',
    photoNotFoundError: 'Foto no encontrada',
    backToGallery: 'Volver a la Galería',
    previousPhoto: 'Foto anterior',
    nextPhoto: 'Foto siguiente',

    // Slideshow
    noPhotosToShow: 'Aún no hay fotos para mostrar',

    // Not found page
    pageNotFound: 'Pagina no encontrada',
    goToGallery: 'Ir a la Galeria',
  },
} as const;

export type Language = keyof typeof translations;
export type TranslationKey = keyof typeof translations.en;
