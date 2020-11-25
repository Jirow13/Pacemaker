﻿using System;

using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.SandBox.CampaignBehaviors;
using TaleWorlds.Localization;

namespace Pacemaker
{
    internal sealed class FastAgingBehavior : CampaignBehaviorBase
    {
        public FastAgingBehavior()
        {
            OnHeroComesOfAge = OnHeroComesOfAgeRM.GetDelegate<OnHeroComesOfAgeDelegate>(CampaignEventDispatcher.Instance);
            OnHeroReachesTeenAge = OnHeroReachesTeenAgeRM.GetDelegate<OnHeroReachesTeenAgeDelegate>(CampaignEventDispatcher.Instance);
            OnHeroGrowsOutOfInfancy = OnHeroGrowsOutOfInfancyRM.GetDelegate<OnHeroGrowsOutOfInfancyDelegate>(CampaignEventDispatcher.Instance);
        }

        public override void RegisterEvents()
        {
            CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, new Action<CampaignGameStarter>(OnSessionLaunched));
            CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnSessionLaunched(CampaignGameStarter starter)
        {
            var educationBehavior = Campaign.Current.CampaignBehaviorManager.GetBehavior<EducationCampaignBehavior>();
            DoEducation = DoEducationRM.GetDelegate<DoEducationDelegate>(educationBehavior);

            var agingBehavior = Campaign.Current.CampaignBehaviorManager.GetBehavior<AgingCampaignBehavior>();
            UpdateHeroDeathProbabilities = UpdateHeroDeathProbabilitiesRM.GetDelegate<UpdateHeroDeathProbabilitiesDelegate>(agingBehavior);

            // Save these for later:
            adultAge = Campaign.Current.Models.AgeModel.HeroComesOfAge;
            teenAge = Campaign.Current.Models.AgeModel.BecomeTeenagerAge;
            childAge = Campaign.Current.Models.AgeModel.BecomeChildAge;
        }

        private void OnDailyTick()
        {
            bool aafEnabled = !Util.NearEqual(Main.Settings!.AgeFactor, 1f, 1e-2);
            PeriodicDeathProbabilityUpdate(aafEnabled);

            if (!CampaignOptions.IsLifeDeathCycleEnabled)
                return;

            /* Send childhood growth stage transition events & perform AAF if enabled */

            // Subtract 1 for the daily tick's implicitly-aged day & the rest is
            // explicit, incremental age to add.
            var birthDayDelta = CampaignTime.Days(Main.Settings.AgeFactor - 1f);

            // And this is just hoisted.
            var oneDay = CampaignTime.Days(1f);

            foreach (var hero in Hero.All)
            {
                if (hero.IsDead)
                    continue;

                // When calculating the prevAge, we must take care to include the day
                // which the daily tick implicitly aged us since we last did this, or
                // else we could miss age transitions. Ergo, prevAge is the age we
                // were as if we were one day younger than our current BirthDay.
                int prevAge = (int)(hero.BirthDay + oneDay).ElapsedYearsUntilNow;

                if (aafEnabled)
                {
                    hero.SetBirthDay(hero.BirthDay - birthDayDelta);
                    hero.CharacterObject.Age = hero.Age;
                }

                // And our new age, if different.
                int newAge = (int)hero.Age;

                // Did a relevant transition in age(s) occur?
                if (newAge > prevAge && prevAge < adultAge && !hero.IsTemplate)
                    ProcessAgeTransition(hero, prevAge, newAge);
            }
        }

        private void ProcessAgeTransition(Hero hero, int prevAge, int newAge)
        {
            // Loop over the aged years (extremely aggressive Days Per Season + AAF
            // could make it multiple), and thus we need to be able to handle the
            // possibility of multiple growth stage events needing to be fired.

            for (int age = prevAge + 1; age <= Math.Min(newAge, adultAge); ++age)
            {
                // Replacement for EducationCampaignBehavior.OnDailyTick()
                //
                // On e1.5.5, they've disabled the EducationCampaignBehavior, but I'm going to
                // continue calling DoEducation so long as the child isn't yet of age, because
                // that seems at worst harmless. What crashes (and not in e1.5.5 because they
                // removed all of the behavior's event listeners, which is what we'll do for
                // our e1.5.4 version) is when their OnHeroComesOfAge event listener runs.
                if (hero.Clan == Clan.PlayerClan && GetChildAgeState(age) != ChildAgeState.Invalid)
                {
                    DoEducation!(hero);

                    // WTF is this doing after the DoEducation call? Magic, or TaleWorlds fucking up?
                    new TextObject("{=Z5qYQV08}Your kin has reached the age of {CHILD.AGE} and needs your guidance on "
                                   + "{?CHILD.GENDER}her{?}his{\\?} development.", null)
                        .SetCharacterProperties("CHILD", hero.CharacterObject, null, false);
                }

                // This replaces AgingCampaignBehavior.OnDailyTick's campaign event triggers:

                if (age == childAge)
                    OnHeroGrowsOutOfInfancy(hero);

                if (age == teenAge)
                    OnHeroReachesTeenAge(hero);

                if (age == adultAge && !hero.IsActive)
                    OnHeroComesOfAge(hero);
            }
        }

        private void PeriodicDeathProbabilityUpdate(bool aafEnabled)
        {
            int daysElapsed = (int)Campaign.Current.CampaignStartTime.ElapsedDaysUntilNow;
            int updatePeriod = Math.Max(1, !aafEnabled
                ? Main.TimeParam.DayPerYear
                : (int)(Main.TimeParam.DayPerYear / Main.Settings!.AgeFactor));

            // Globally update death probabilities every year of accumulated age
            if (daysElapsed % updatePeriod == 0)
                UpdateHeroDeathProbabilities!();
        }

        private ChildAgeState GetChildAgeState(int age)
        {
            return age switch
            {
                2  => ChildAgeState.Year2,
                5  => ChildAgeState.Year5,
                8  => ChildAgeState.Year8,
                12 => ChildAgeState.Year12,
                15 => ChildAgeState.Year15,
                17 => ChildAgeState.Year17,
                _  => ChildAgeState.Invalid,
            };
        }

        private enum ChildAgeState
        {
            Invalid = -1,
            Year2,
            Year5,
            Year8,
            Year12,
            Year15,
            Year17
        }

        // Year thresholds (cached):
        private int adultAge;
        private int teenAge;
        private int childAge;

        // Delegates, delegates, delegates...
        private delegate void DoEducationDelegate(Hero child);
        private delegate void UpdateHeroDeathProbabilitiesDelegate();
        private delegate void OnHeroComesOfAgeDelegate(Hero hero);
        private delegate void OnHeroReachesTeenAgeDelegate(Hero hero);
        private delegate void OnHeroGrowsOutOfInfancyDelegate(Hero hero);

        private DoEducationDelegate? DoEducation;
        private UpdateHeroDeathProbabilitiesDelegate? UpdateHeroDeathProbabilities;
        private readonly OnHeroComesOfAgeDelegate OnHeroComesOfAge;
        private readonly OnHeroReachesTeenAgeDelegate OnHeroReachesTeenAge;
        private readonly OnHeroGrowsOutOfInfancyDelegate OnHeroGrowsOutOfInfancy;

        // Reflection for triggering campaign events & death probability updates & childhood education stage processing:
        private static readonly Reflect.DeclaredMethod<EducationCampaignBehavior> DoEducationRM = new("DoEducation");
        private static readonly Reflect.DeclaredMethod<AgingCampaignBehavior> UpdateHeroDeathProbabilitiesRM = new("UpdateHeroDeathProbabilities");
        private static readonly Reflect.DeclaredMethod<CampaignEventDispatcher> OnHeroComesOfAgeRM = new("OnHeroComesOfAge");
        private static readonly Reflect.DeclaredMethod<CampaignEventDispatcher> OnHeroReachesTeenAgeRM = new("OnHeroReachesTeenAge");
        private static readonly Reflect.DeclaredMethod<CampaignEventDispatcher> OnHeroGrowsOutOfInfancyRM = new("OnHeroGrowsOutOfInfancy");
    }
}
