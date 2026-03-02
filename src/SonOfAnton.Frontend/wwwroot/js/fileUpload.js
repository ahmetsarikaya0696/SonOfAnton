// SonOfAnton File Upload JS Interop
// .md dosya okuma ve file input tetikleme

window.soaFileUpload = {
    /**
     * Gizli file input'u programatik olarak tetikler
     * @param {string} inputId - File input element ID
     */
    triggerFileInput: function (inputId) {
        const input = document.getElementById(inputId);
        if (input) {
            input.click();
        }
    },

    /**
     * File input'tan seçilen dosyayı text olarak okur
     * @param {string} inputId - File input element ID
     * @returns {Promise<string|null>} Dosya içeriği veya null
     */
    readFileAsText: async function (inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files || input.files.length === 0) {
            return null;
        }

        const file = input.files[0];
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error);
            reader.readAsText(file);
        });
    },

    /**
     * File input'tan dosya adını döner
     * @param {string} inputId - File input element ID
     * @returns {string|null} Dosya adı
     */
    getFileName: function (inputId) {
        const input = document.getElementById(inputId);
        if (!input || !input.files || input.files.length === 0) {
            return null;
        }
        return input.files[0].name;
    },

    /**
     * Drag & drop alanına drop edilen dosyayı text olarak okur
     * Blazor IJSRuntime'dan çağrılır, dotNetHelper üzerinden callback yapar
     * @param {string} dropZoneId - Drop zone element ID
     * @param {object} dotNetHelper - .NET object reference
     * @param {string} callbackMethod - .NET callback method name
     */
    setupDropZone: function (dropZoneId, dotNetHelper, callbackMethod) {
        const dropZone = document.getElementById(dropZoneId);
        if (!dropZone) return;

        dropZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.add('drag-over');
        });

        dropZone.addEventListener('dragleave', (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('drag-over');
        });

        dropZone.addEventListener('drop', async (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('drag-over');

            if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
                const file = e.dataTransfer.files[0];
                if (file.name.endsWith('.md') || file.name.endsWith('.markdown') || file.name.endsWith('.txt')) {
                    const reader = new FileReader();
                    reader.onload = async () => {
                        await dotNetHelper.invokeMethodAsync(callbackMethod, reader.result, file.name);
                    };
                    reader.readAsText(file);
                } else {
                    await dotNetHelper.invokeMethodAsync(callbackMethod, null, file.name);
                }
            }
        });
    }
};
