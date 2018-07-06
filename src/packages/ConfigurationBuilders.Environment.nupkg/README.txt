########################################################################################################################
##                                                                                                                    ##
##                             Microsoft.Configuration.ConfigurationBuilders.Environment                              ##
##                                                                                                                    ##
########################################################################################################################

WARNING: Double check config builder attributes in your configuraiton files. Some attributes may have been changed
         when upgrading this package.

If this is the first time you are installing this configuration builder into your
project, feel free to click the 'X' on this tab to close and continue on.

However, if you are updating this package from a version earlier than 1.0.2... you may want to double check the
declarations for your config builders, because we might have lost changes you made to the default declarations
that were created during the original install.

The upgrade mechanism for nuget is actually just Uninstall/Install, and prior to 1.0.2, this package would delete
the config builder declaration that it created upon install. Makes sense. But if you made any changes to that
declaration without changing the name of it, we didn't bother to make note of that. So the 'install' phase of
updating will declare the default config builder with the default parameters again. (If you did change the name
to something other than the default - Environment - then we did not delete your declaration when uninstalling.)

Starting in version 1.0.2, we have a way to stash old declarations aside and restore them upon install. Sorry for the
inconvenience. There should be no such troubles with future upgrades.
