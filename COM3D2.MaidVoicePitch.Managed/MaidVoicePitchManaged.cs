using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace CM3D2.MaidVoicePitch.Managed
{
	namespace Callbacks
	{
		namespace TBody
		{

			public static class LateUpdate
			{
				public delegate void Callback(global::TBody that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::TBody that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}

			public static class MoveHeadAndEye
			{
				public delegate void Callback(global::TBody that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::TBody that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}

		namespace BoneMorph_
		{
			public static class Blend
			{
				public delegate void Callback(global::BoneMorph_ that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::BoneMorph_ that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}

		namespace AudioSourceMgr
		{
			public static class Play
			{
				public delegate void Callback(global::AudioSourceMgr that, float f_fFadeTime, bool loop);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::AudioSourceMgr that, float f_fFadeTime, bool loop)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that, f_fFadeTime, loop);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}

			public static class PlayOneShot
			{
				public delegate void Callback(global::AudioSourceMgr that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::AudioSourceMgr that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}

		namespace CharacterMgr
		{
			public static class PresetSet
			{
				public delegate void Callback(global::CharacterMgr that, Maid f_maid, global::CharacterMgr.Preset f_prest);

				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::CharacterMgr that, Maid f_maid, global::CharacterMgr.Preset f_prest, bool f_man)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that, f_maid, f_prest);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}

		namespace DynamicSkirtBone
		{
			public static class PreUpdateSelf
			{
				public delegate void Callback(global::DynamicSkirtBone that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::DynamicSkirtBone that)
				{

					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}

			public static class PostUpdateSelf
			{
				public delegate void Callback(global::DynamicSkirtBone that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::DynamicSkirtBone that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}

		namespace jiggleBone
		{
			public static class PreLateUpdateSelf
			{
				public delegate void Callback(global::jiggleBone that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::jiggleBone that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}

		namespace jiggleBone
		{
			public static class PostLateUpdateSelf
			{
				public delegate void Callback(global::jiggleBone that);
				public static Callbacks<Callback> Callbacks = new Callbacks<Callback>();

				public static void Invoke(global::jiggleBone that)
				{
					try
					{
						for (int i = 0; i < Callbacks.ArrayOfVals.Length; i++)
						{
							Callbacks.ArrayOfVals[i](that);
						}
					}
					catch (Exception e)
					{
						Helper.ShowException(e);
					}
				}
			}
		}
		//Calling values in a normal dictionary returns an enumerable which has high GC allocs. Given the amount of calls for that property, we made a cached dictionary that does basically the same thing but way faster without any GC allocs.
		public class Callbacks<TValue> : SortedDictionary<string, TValue>
		{
			private TValue[] values = new TValue[0];

			public TValue[] ArrayOfVals
			{
				get
				{
					if (base.Count != values.Length)
					{
						//Console.WriteLine("Updated ArrayOfVals");

						Array.Resize(ref values, Count);
						base.Values.CopyTo(values, 0);
					}

					return values;
				}
			}
		}
	}
}
