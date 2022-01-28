# How to contribute

One of the easiest ways to contribute is to participate in discussions and discuss issues. You can also contribute by submitting pull requests with code changes.

## General feedback and discussions?
Please start a discussion on the [ConfigurationBuilders repo issue tracker](https://github.com/aspnet/MicrosoftConfigurationBuilders/issues).

## Bugs and feature requests?
For non-security related bugs please log a new issue on the [ConfigurationBuilders repo issue tracker](https://github.com/aspnet/MicrosoftConfigurationBuilders/issues).

## Reporting security issues and bugs
Security issues and bugs should be reported privately, via email, to the Microsoft Security Response Center (MSRC)  secure@microsoft.com. You should receive a response within 24 hours. If for some reason you do not, please follow up via email to ensure we received your original message. Further information, including the MSRC PGP key, can be found in the [Security TechCenter](https://technet.microsoft.com/en-us/security/ff852094.aspx).

## Filing issues
When filing issues, please use our [bug filing templates](https://github.com/aspnet/MicrosoftConfigurationBuilders/wiki/Functional-bug-template).
The best way to get your bug fixed is to be as detailed as you can be about the problem.
Providing a minimal project with steps to reproduce the problem is ideal.
Here are questions you can answer before you file a bug to make sure you're not missing any important information.

1. Did you read the [documentation](https://github.com/aspnet/MicrosoftConfigurationBuilders/wiki)?
2. Did you include the snippet of broken code in the issue?
3. What are the *EXACT* steps to reproduce this problem?
4. What version Powershell are you using?
5. What version of VS (including update version) are you using?

GitHub supports [markdown](https://help.github.com/articles/github-flavored-markdown/), so when filing bugs make sure you check the formatting before clicking submit.

## Contributing code and content

**Obtaining the source code**

If you are an outside contributer, please fork the repository. See the GitHub documentation for [forking a repo](https://help.github.com/articles/fork-a-repo/) if you have any questions about this. 

**Building the source code**

To build the project and run tests, you need to register the public key token for verification skip:

1. Open a Visual Studio developer prompt as administrator.
2. Run `sn -Vr *,31bf3856ad364e35
3. Run `build.cmd` to restore NuGet packages, build the solution, and run tests.

**Submitting a pull request**

You will need to sign a [Contributor License Agreement](https://cla.opensource.microsoft.com//) when submitting your pull request. To complete the Contributor License Agreement (CLA), you will need to follow the instructions provided by the CLA bot when you send the pull request. This needs to only be done once for any Microsoft OSS project.

If you don't know what a pull request is read this article: https://help.github.com/articles/using-pull-requests. Make sure the respository can build and all tests pass. 

**Commit/Pull Request Format**

```
Summary of the changes (Less than 80 chars)
 - Detail 1
 - Detail 2

Addresses #bugnumber (in this specific format)
```

**Tests**

-  Tests need to be provided for every bug/feature that is completed.
-  Tests only need to be present for issues that need to be verified by QA (e.g. not tasks)
-  If there is a scenario that is far too hard to test there does not need to be a test for it.
  - "Too hard" is determined by the team as a whole.

**Feedback**

Your pull request will now go through extensive checks by the subject matter experts on our team. Please be patient; we have hundreds of pull requests across all of our repositories. Update your pull request according to feedback until it is approved by one of the ASP.NET team members. After that, one of our team members will add the pull request to **dev**.