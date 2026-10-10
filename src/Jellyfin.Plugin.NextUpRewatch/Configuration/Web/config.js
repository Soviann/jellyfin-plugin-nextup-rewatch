const pluginId = '9b84a277-2f3b-48cf-9a99-b1d5c7f897e1';

export default function (view) {
    view.addEventListener('viewshow', () => {
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(pluginId).then((config) => {
            view.querySelector('#chkEnableFilter').checked = config.EnableFilter !== false;
            view.querySelector('#chkEnableDebugLogging').checked = config.EnableDebugLogging === true;
            Dashboard.hideLoadingMsg();
        });
    });

    view.querySelector('#NextUpRewatchConfigForm').addEventListener('submit', (e) => {
        e.preventDefault();
        Dashboard.showLoadingMsg();

        ApiClient.getPluginConfiguration(pluginId).then((config) => {
            config.EnableFilter = view.querySelector('#chkEnableFilter').checked;
            config.EnableDebugLogging = view.querySelector('#chkEnableDebugLogging').checked;

            ApiClient.updatePluginConfiguration(pluginId, config).then((result) => {
                Dashboard.processPluginConfigurationUpdateResult(result);
            });
        });

        return false;
    });
}
