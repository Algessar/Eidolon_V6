// using EidolonCore.ECS;
// using EidolonCore.Rendering;
//
// namespace EiolonCore.RenderSystem ??
//
// public class RenderSystem
// {
//     private World _world;
//     private IRenderer _renderer;
//
//     public RenderSystem(World world, IRenderer renderer)
//     {
//         _world = world;
//         _renderer = renderer;
//     }
//
//     public void Update()
//     {
//         var entities = _world.GetEntitiesWith<Transform>().Where(e => 
//             _world.HasComponent<MeshRenderer>(e));
//
//         foreach (var entity in entities)
//         {
//             var transform = _world.GetComponent<Transform>(entity);
//             var renderer = _world.GetComponent<MeshRenderer>(entity);
//
//             var drawData = new DrawData
//             {
//                 Mesh = renderer.Mesh,
//                 Material = renderer.Material,
//                 ModelMatrix = transform.GetWorldMatrix()
//             };
//         }
//     }
//     
//     //NOTE: 
//     /*
//      * Now the question is how this actually works. Update is straightforward enough, except
//      * for the DrawData. 
//      */
//     
// }