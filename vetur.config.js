/** @type {import('vls').VeturConfig} */
module.exports = {
    settings: {
        "vetur.useWorkspaceDependencies": true,
        "vetur.experimental.templateInterpolationService": true,
    },
    projects: [
        {
            root: "./HamStatus.Vue/src",
            globalComponents: [
                "../node_modules/vuetify/lib/components/**",
            ],
        },
    ],
};
