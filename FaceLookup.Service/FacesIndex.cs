using FaceLookup.Common;
using FaceLookup.Service.Interfaces;
using HNSW.Net;
using Keras.Models;
using Keras.PreProcessing.Image;
using Numpy;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FaceLookup.Service
{
    public class FacesIndex<T> where T : IFaceIndexsItem
    {
        public FacesIndex(string face2VectorModelPath, IFacesIndexDataProvider dataProvider)
        {
            _face2VectorModelPath = face2VectorModelPath;
            _dataProvider = dataProvider;
            _distanceFunction = CosineDistance.NonOptimized;
        }



        public bool Init()
        {
            var parameters = new SmallWorld<float[], float>.Parameters()
            {
                //M = 15,
                //LevelLambda = 1 / Math.Log(15),             
            };
            
            _indexGraph = new SmallWorld<float[], float>(_distanceFunction, DefaultRandomGenerator.Instance, parameters);

            _image2VectorModel = Sequential.LoadModel(_face2VectorModelPath);
            return true;
        }

        private readonly IFacesIndexDataProvider _dataProvider;
        private readonly string _face2VectorModelPath;
        private readonly Func<float[], float[], float> _distanceFunction;
        private BaseModel _image2VectorModel;
        private SmallWorld<float[], float> _indexGraph;

        public void AddBulkFaces(IEnumerable<T> faces)
        {
            var butchSize = 100;
            var batches = CollectionHelper.Split(faces, butchSize);

            var collection = new List<float[]>();
            var itemsCollection = new List<IFaceIndexsItem>();
            foreach (var batch in batches)
            {
                var batchImages = batch.Select(info => ReadImage(info.FaceSource)).ToArray();
                var imagesArray = np.array(batchImages);
                var imageLatentVector = _image2VectorModel.Predict(imagesArray);

                for (int i = 0; i < batch.Count; i++)
                {
                    var latentVector = imageLatentVector[i].GetData<float>();
                    var item = batch[i];
                    item.FaceVector = latentVector;

                    itemsCollection.Add(item);

                    collection.Add(latentVector);
                }
            }

            // add to index
            var ids = _indexGraph.AddItems(collection);
            for (int i = 0; i < itemsCollection.Count; i++)
            {
                var item = itemsCollection[i];
                var id = ids[i];
                item.FaceIndexId = id;
            }
            //_imagesIndexItems.AddRange(itemsCollection);
        }

        private NDarray ReadImage(string imagePath)
        {
            var image = ImageUtil.LoadImg(imagePath, color_mode: "rgb", target_size: new Keras.Shape(64, 64));
            var imageArray = (ImageUtil.ImageToArray(image) as NDarray) / 255F;
            return imageArray.reshape(64, 64, 3);
        }
    }
}
