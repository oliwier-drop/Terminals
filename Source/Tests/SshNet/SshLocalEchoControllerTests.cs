// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (c) oliwier-drop and contributors - fork-authored code.
// See LICENSE.md and FORK-AUTHORED.md at the repository root.
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Terminals.Plugins.SshNet;

namespace Tests.SshNet
{
    [TestClass]
    public class SshLocalEchoControllerTests
    {
        [TestMethod]
        public void TryCreatePrintableEcho_NormalPrompt_AllowsEcho()
        {
            var controller = new SshLocalEchoController();

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('a', "user@host:$ ", true, out localEcho);

            Assert.IsTrue(allowed);
            Assert.AreEqual("a", localEcho);
        }

        [TestMethod]
        public void TryCreatePrintableEcho_PasswordPrompt_DisablesEcho()
        {
            var controller = new SshLocalEchoController();

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('s', "Password: ", true, out localEcho);

            Assert.IsFalse(allowed);
            Assert.IsNull(localEcho);
        }

        [TestMethod]
        public void TryCreatePrintableEcho_PinPrompt_DisablesEcho()
        {
            var controller = new SshLocalEchoController();

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('1', "PIN: ", true, out localEcho);

            Assert.IsFalse(allowed);
            Assert.IsNull(localEcho);
        }

        [TestMethod]
        public void TryCreatePrintableEcho_PromptContainingPinSubstring_AllowsEcho()
        {
            var controller = new SshLocalEchoController();

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('a', "alpine@host:$ ", true, out localEcho);

            Assert.IsTrue(allowed);
            Assert.AreEqual("a", localEcho);
        }

        [TestMethod]
        public void FilterServerOutput_MatchingEcho_RemovesEcho()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");

            string filtered = controller.FilterServerOutput("a");

            Assert.AreEqual(string.Empty, filtered);
            Assert.IsFalse(controller.HasPendingEcho);
        }

        [TestMethod]
        public void FilterServerOutput_PartialMatchingEcho_WaitsForRemainder()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");
            controller.RegisterPrintableEcho("b");

            Assert.AreEqual(string.Empty, controller.FilterServerOutput("a"));
            Assert.IsTrue(controller.HasPendingEcho);
            Assert.AreEqual(" prompt", controller.FilterServerOutput("b prompt"));
            Assert.IsFalse(controller.HasPendingEcho);
        }

        [TestMethod]
        public void FilterServerOutput_Mismatch_PassesOutputAndResetsPendingEcho()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");

            string filtered = controller.FilterServerOutput("z");

            Assert.AreEqual("z", filtered);
            Assert.IsFalse(controller.HasPendingEcho);
        }

        [TestMethod]
        public void FilterServerOutput_AlternateScreen_DisablesPrintableEcho()
        {
            var controller = new SshLocalEchoController();
            controller.FilterServerOutput("\x1b[?1049h");

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('a', "$ ", true, out localEcho);

            Assert.IsFalse(allowed);
        }

        [TestMethod]
        public void FilterServerOutput_AlternateScreen1047_DisablesPrintableEcho()
        {
            var controller = new SshLocalEchoController();
            controller.FilterServerOutput("\x1b[?1047h");

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('a', "$ ", true, out localEcho);

            Assert.IsFalse(allowed);
        }

        [TestMethod]
        public void FilterServerOutput_AlternateScreenExit_AllowsPrintableEcho()
        {
            var controller = new SshLocalEchoController();
            controller.FilterServerOutput("\x1b[?1049h");
            controller.FilterServerOutput("\x1b[?1049l");

            string localEcho;
            bool allowed = controller.TryCreatePrintableEcho('a', "$ ", true, out localEcho);

            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public void TryCreateBackspaceEcho_WithoutPendingPrintable_DisablesEcho()
        {
            var controller = new SshLocalEchoController();

            string localEcho;
            bool allowed = controller.TryCreateBackspaceEcho("\x7f", "dropadmin@SRV", true, out localEcho);

            Assert.IsFalse(allowed);
            Assert.IsNull(localEcho);
        }

        [TestMethod]
        public void TryCreateBackspaceEcho_WithPendingPrintable_AllowsEcho()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");

            string localEcho;
            bool allowed = controller.TryCreateBackspaceEcho("\x7f", "dropadmin@SRVa", true, out localEcho);

            Assert.IsTrue(allowed);
            Assert.AreEqual("\b \b", localEcho);
        }

        [TestMethod]
        public void CompleteBackspaceUndo_RemovesLastPrintableAndFiltersServerErase()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");
            controller.CompleteBackspaceUndo("\x7f");

            Assert.AreEqual(string.Empty, controller.FilterServerOutput("\b \b"));
            Assert.IsFalse(controller.HasPendingEcho);
        }

        [TestMethod]
        public void FilterServerOutput_BackspaceEchoVariant_RemovesEcho()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");
            controller.CompleteBackspaceUndo("\x7f");

            string filtered = controller.FilterServerOutput("\b \b");

            Assert.AreEqual(string.Empty, filtered);
            Assert.IsFalse(controller.HasPendingEcho);
        }

        [TestMethod]
        public void FilterServerOutput_SplitBackspaceEchoVariant_WaitsForRemainder()
        {
            var controller = new SshLocalEchoController();
            controller.RegisterPrintableEcho("a");
            controller.CompleteBackspaceUndo("\x7f");

            Assert.AreEqual(string.Empty, controller.FilterServerOutput("\b"));
            Assert.IsTrue(controller.HasPendingEcho);
            Assert.AreEqual(" prompt", controller.FilterServerOutput(" \b prompt"));
            Assert.IsFalse(controller.HasPendingEcho);
        }
    }
}
